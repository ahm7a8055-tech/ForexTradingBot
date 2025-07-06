document.addEventListener('DOMContentLoaded', () => {
    const settingsForm = document.getElementById('settingsForm');
    const settingsContainer = document.getElementById('settingsContainer');
    const saveAllSettingsButton = document.getElementById('saveAllSettingsButton');
    const loadingOverlay = document.getElementById('loading-overlay-settings');

    const SENSITIVE_MASK = "*****";

    // Store initial values to detect changes
    let initialSettings = {};

    async function loadSettings() {
        if (loadingOverlay) loadingOverlay.style.display = 'flex';
        if (saveAllSettingsButton) saveAllSettingsButton.disabled = true;

        try {
            const response = await fetch('/api/settings/all');
            if (!response.ok) {
                if (response.status === 401) window.location.href = '/login.html';
                throw new Error(`Failed to fetch settings: ${response.status}`);
            }
            const settings = await response.json();
            renderSettings(settings);
            if (saveAllSettingsButton) saveAllSettingsButton.disabled = false;
        } catch (error) {
            console.error('Error loading settings:', error);
            if (typeof toastr !== 'undefined') toastr.error('Could not load settings. Please try again or re-login.');
            if (settingsContainer) settingsContainer.innerHTML = '<p class="text-danger">Failed to load settings.</p>';
        } finally {
            if (loadingOverlay) loadingOverlay.style.display = 'none';
        }
    }

    function renderSettings(settings) {
        if (!settingsContainer) return;
        settingsContainer.innerHTML = ''; // Clear previous
        initialSettings = {}; // Reset initial settings cache

        // Group settings by prefix (e.g., "Admin", "ConnectionStrings", "TelegramPanel")
        const groupedSettings = settings.reduce((acc, setting) => {
            const parts = setting.key.split(':');
            const groupName = parts.length > 1 ? parts[0] : 'General';
            if (!acc[groupName]) {
                acc[groupName] = [];
            }
            acc[groupName].push(setting);
            initialSettings[setting.key] = setting.isSensitive && setting.isPersistedInDb && !setting.isOverriddenByEnvironment
                                            ? null // For sensitive, persisted, non-overridden, we don't know the actual value to compare, so assume it might change
                                            : setting.value; // Store raw value from DB/config if available and not overridden
            return acc;
        }, {});

        for (const groupName in groupedSettings) {
            const groupDiv = document.createElement('div');
            groupDiv.className = 'settings-section';

            const groupTitle = document.createElement('h3');
            groupTitle.innerHTML = `<i class="fas fa-layer-group"></i> ${formatGroupName(groupName)}`;
            groupDiv.appendChild(groupTitle);

            groupedSettings[groupName].forEach(setting => {
                const formGroup = document.createElement('div');
                formGroup.className = 'form-group mb-3';

                const label = document.createElement('label');
                label.htmlFor = `setting-${setting.key.replace(/:/g, '_')}`;
                label.textContent = setting.key;
                if (setting.isSensitive) {
                    label.innerHTML += ' <i class="fas fa-eye-slash text-warning" title="Sensitive Value"></i>';
                }
                formGroup.appendChild(label);

                const inputType = setting.isSensitive ? 'password' : 'text';
                // For boolean, could use a checkbox, for lists (AdminUserIds), a textarea or special input
                // For now, most are text/password
                let inputElement;

                if (setting.key.toLowerCase().includes("enabledebugmode") || setting.key.toLowerCase().includes("istestnet") || setting.key.toLowerCase().includes("enablerssmodule") || setting.key.toLowerCase().includes("enableforwardingmodule") ) {
                    inputElement = document.createElement('select');
                    inputElement.className = 'form-select';
                    const trueOption = document.createElement('option');
                    trueOption.value = "true";
                    trueOption.textContent = "True";
                    const falseOption = document.createElement('option');
                    falseOption.value = "false";
                    falseOption.textContent = "False";
                    inputElement.appendChild(trueOption);
                    inputElement.appendChild(falseOption);

                    let currentEffectiveValue = setting.isOverriddenByEnvironment ? setting.value : (setting.isPersistedInDb ? setting.value : setting.value); // setting.value is raw from server
                    if (setting.isSensitive && setting.isPersistedInDb && !setting.isOverriddenByEnvironment) {
                        // We don't get the actual value for sensitive persisted fields, so we can't set the select.
                        // The user must re-enter if they want to change.
                    } else {
                         inputElement.value = String(currentEffectiveValue).toLowerCase() === "true" ? "true" : "false";
                    }

                } else {
                    inputElement = document.createElement('input');
                    inputElement.type = inputType;
                    inputElement.className = 'form-control';
                    // DisplayValue is masked if sensitive & from DB/Config. Value is raw.
                    // If overridden, value is plaintext from env.
                    if (setting.isOverriddenByEnvironment) {
                        inputElement.value = setting.value || "";
                    } else {
                        if (setting.isSensitive && setting.isPersistedInDb) {
                            inputElement.placeholder = SENSITIVE_MASK + " (Enter new value to change)";
                            inputElement.value = ""; // Don't prefill password fields with mask
                        } else {
                             inputElement.value = setting.value || ""; // Value is raw from DB/Config
                        }
                    }
                }

                inputElement.id = `setting-${setting.key.replace(/:/g, '_')}`;
                inputElement.name = setting.key;
                inputElement.disabled = setting.isOverriddenByEnvironment;
                formGroup.appendChild(inputElement);

                if (setting.description) {
                    const small = document.createElement('small');
                    small.className = 'form-text text-muted';
                    small.textContent = setting.description;
                    formGroup.appendChild(small);
                }

                if (setting.isOverriddenByEnvironment) {
                    const overriddenNote = document.createElement('p');
                    overriddenNote.className = 'overridden-value mt-1';
                    overriddenNote.innerHTML = `<i class="fas fa-exclamation-triangle text-info"></i> This value is currently overridden by an environment variable (<span class="info-value">${setting.isSensitive ? SENSITIVE_MASK : setting.value}</span>) and cannot be changed here.`;
                    formGroup.appendChild(overriddenNote);
                } else if (setting.isPersistedInDb && setting.isSensitive) {
                     const persistedNote = document.createElement('p');
                    persistedNote.className = 'overridden-value mt-1';
                    persistedNote.innerHTML = `<i class="fas fa-info-circle text-info"></i> This sensitive value is set in the database. Enter a new value to change it. Last updated: ${setting.lastModifiedUtc ? new Date(setting.lastModifiedUtc).toLocaleString() : 'N/A'}`;
                    formGroup.appendChild(persistedNote);
                } else if (setting.isPersistedInDb) {
                     const persistedNote = document.createElement('p');
                    persistedNote.className = 'overridden-value mt-1';
                    persistedNote.innerHTML = `<i class="fas fa-database text-success"></i> Using value from database. Last updated: ${setting.lastModifiedUtc ? new Date(setting.lastModifiedUtc).toLocaleString() : 'N/A'}`;
                    formGroup.appendChild(persistedNote);
                } else {
                    const defaultNote = document.createElement('p');
                    defaultNote.className = 'overridden-value mt-1';
                    defaultNote.innerHTML = `<i class="fas fa-cogs text-secondary"></i> Using default value from application configuration.`;
                    formGroup.appendChild(defaultNote);
                }


                groupDiv.appendChild(formGroup);
            });
            settingsContainer.appendChild(groupDiv);
        }
    }

    function formatGroupName(name) {
        // Example: "TelegramPanel" -> "Telegram Panel Settings"
        return name.replace(/([A-Z])/g, ' $1').trim() + " Settings";
    }

    if (settingsForm && saveAllSettingsButton) {
        settingsForm.addEventListener('submit', async (event) => {
            event.preventDefault();
            if (loadingOverlay) loadingOverlay.style.display = 'flex';
            saveAllSettingsButton.disabled = true;
            saveAllSettingsButton.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Saving...';

            const settingsToUpdate = {};
            let changesMade = false;

            const inputs = settingsForm.querySelectorAll('input[name], select[name]');
            inputs.forEach(input => {
                if (input.disabled) return; // Skip overridden by env

                const key = input.name;
                const currentValue = input.value;

                // For sensitive fields that were masked (and thus empty), only include if user typed something new.
                // For non-sensitive, or sensitive but not masked (e.g. new value), include if different from initial.
                // initialSettings[key] being null means it was a sensitive, persisted, non-overridden value.
                let originalValue = initialSettings[key];
                if (originalValue === null && input.type === 'password' && currentValue === '') {
                    // User didn't type anything into a sensitive field that was previously set, so don't send update for it
                } else if (originalValue !== currentValue) {
                    settingsToUpdate[key] = currentValue;
                    changesMade = true;
                }
            });

            if (!changesMade) {
                if (typeof toastr !== 'undefined') toastr.info('No changes detected to save.');
                if (loadingOverlay) loadingOverlay.style.display = 'none';
                saveAllSettingsButton.disabled = false;
                saveAllSettingsButton.innerHTML = '<i class="fas fa-save"></i> Save All Changes';
                return;
            }

            try {
                const response = await fetch('/api/settings/update', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ settingsToUpdate })
                });

                if (response.ok) { // Expects 204 NoContent
                    if (typeof toastr !== 'undefined') toastr.success('Settings updated successfully! Some changes may require an application restart to take full effect.');
                    // Reload settings to show updated state (e.g. LastModifiedUtc)
                    await loadSettings();
                } else {
                    const errorData = await response.json().catch(() => ({ message: `Failed to update settings. Status: ${response.status}` }));
                    if (typeof toastr !== 'undefined') toastr.error(errorData.message || 'An error occurred.');
                }
            } catch (error) {
                console.error('Error saving settings:', error);
                if (typeof toastr !== 'undefined') toastr.error('An unexpected error occurred while saving settings.');
            } finally {
                if (loadingOverlay) loadingOverlay.style.display = 'none';
                saveAllSettingsButton.disabled = false;
                saveAllSettingsButton.innerHTML = '<i class="fas fa-save"></i> Save All Changes';
            }
        });
    }

    // Initial load
    loadSettings();
});
