document.addEventListener('DOMContentLoaded', () => {
    const configForm = document.getElementById('configForm');
    const botTokenInput = document.getElementById('botToken');
    const dbConnInput = document.getElementById('dbConn');
    const redisConnInput = document.getElementById('redisConn');
    const testConnectionButton = document.getElementById('testConnectionButton');
    const saveConfigurationButton = document.getElementById('saveConfigurationButton');

    // Status icons (assuming Font Awesome is used)
    const botTokenStatus = document.getElementById('botTokenStatus');
    const dbConnStatus = document.getElementById('dbConnStatus');
    const redisConnStatus = document.getElementById('redisConnStatus');
    const telegramBotUsernameElem = document.getElementById('telegramBotUsername');


    if (configForm) {
        if (testConnectionButton) {
            testConnectionButton.addEventListener('click', async () => {
                await testConnections();
            });
        }

        if (saveConfigurationButton) {
            saveConfigurationButton.addEventListener('click', async () => {
                await saveConfiguration();
            });
            // Initially disable save button until tests pass
            saveConfigurationButton.disabled = true;
        }
    }

    function setStatusIcon(element, status, message = '') {
        if (!element) return;
        element.className = 'status-icon fas'; // Reset classes
        if (status === 'OK') {
            element.classList.add('fa-check-circle');
            element.title = 'Connection Successful';
            if (typeof toastr !== 'undefined') toastr.success(message || 'Test successful!');
        } else if (status === 'Error') {
            element.classList.add('fa-times-circle');
            element.title = `Error: ${message}`;
            if (typeof toastr !== 'undefined') toastr.error(message || 'Test failed!');
        } else if (status === 'Testing') {
            element.classList.add('fa-spinner', 'fa-spin');
            element.title = 'Testing...';
        } else if (status === 'NotProvided') {
             element.classList.add('fa-minus-circle');
             element.title = 'Not Provided';
        } else { // Clear
            element.title = '';
        }
    }

    function resetStatusIcons() {
        setStatusIcon(botTokenStatus, null);
        setStatusIcon(dbConnStatus, null);
        setStatusIcon(redisConnStatus, null);
        if(telegramBotUsernameElem) telegramBotUsernameElem.textContent = 'N/A';
        if(saveConfigurationButton) saveConfigurationButton.disabled = true;
    }


    async function testConnections() {
        if (!botTokenInput || !dbConnInput || !redisConnInput || !testConnectionButton) return;

        resetStatusIcons();
        setLoading(testConnectionButton, true, 'Testing...');

        const payload = {
            botToken: botTokenInput.value.trim(),
            dbConn: dbConnInput.value.trim(),
            redisConn: redisConnInput.value.trim()
        };

        if (!payload.botToken || !payload.dbConn) {
            if (typeof toastr !== 'undefined') toastr.warning('Bot Token and DB Connection String are required to run tests.');
            setLoading(testConnectionButton, false, 'Test Connections');
            return;
        }

        setStatusIcon(botTokenStatus, 'Testing');
        setStatusIcon(dbConnStatus, 'Testing');
        if(payload.redisConn) setStatusIcon(redisConnStatus, 'Testing');
        else setStatusIcon(redisConnStatus, 'NotProvided');


        try {
            const response = await fetch('/api/config/test', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            const results = await response.json();

            if (!response.ok) {
                 if (typeof toastr !== 'undefined') toastr.error(`Error testing connections: ${results.message || response.status}`);
                 // Still update icons based on partial results if available
            }

            setStatusIcon(botTokenStatus, results.telegramStatus, results.telegramError || results.botUsername || results.telegramStatus);
            if (results.botUsername && telegramBotUsernameElem) {
                telegramBotUsernameElem.textContent = results.botUsername;
            }
            setStatusIcon(dbConnStatus, results.databaseStatus, results.databaseError || results.databaseStatus);

            if (payload.redisConn) {
                setStatusIcon(redisConnStatus, results.redisStatus, results.redisError || results.redisStatus);
            } else {
                 setStatusIcon(redisConnStatus, 'NotProvided');
            }

            // Enable save button only if all essential tests pass
            if (results.databaseStatus === 'OK' && results.telegramStatus === 'OK' && (results.redisStatus === 'OK' || results.redisStatus === 'Not Provided' || results.redisStatus === 'Not Tested')) {
                if(saveConfigurationButton) saveConfigurationButton.disabled = false;
                if (typeof toastr !== 'undefined') toastr.success('All essential connection tests passed. You can now save.');
            } else {
                if(saveConfigurationButton) saveConfigurationButton.disabled = true;
                if (typeof toastr !== 'undefined') toastr.warning('One or more connection tests failed. Please review.');
            }

        } catch (error) {
            console.error('Test connections error:', error);
            if (typeof toastr !== 'undefined') toastr.error('An unexpected error occurred while testing connections.');
            setStatusIcon(botTokenStatus, 'Error', 'Unexpected error');
            setStatusIcon(dbConnStatus, 'Error', 'Unexpected error');
            if(payload.redisConn) setStatusIcon(redisConnStatus, 'Error', 'Unexpected error');
        } finally {
            setLoading(testConnectionButton, false, 'Test Connections');
        }
    }

    async function saveConfiguration() {
        if (!botTokenInput || !dbConnInput || !redisConnInput || !saveConfigurationButton) return;

        setLoading(saveConfigurationButton, true, 'Saving...');

        const payload = {
            botToken: botTokenInput.value.trim(),
            dbConn: dbConnInput.value.trim(),
            redisConn: redisConnInput.value.trim()
        };

        if (!payload.botToken || !payload.dbConn) {
            if (typeof toastr !== 'undefined') toastr.warning('Bot Token and DB Connection String are required to save.');
            setLoading(saveConfigurationButton, false, 'Save Configuration');
            return;
        }

        try {
            const response = await fetch('/api/config/save', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            const result = await response.json();

            if (response.ok) {
                if (typeof toastr !== 'undefined') toastr.success(result.message || 'Configuration saved (placeholder)! Redirecting to dashboard...');
                setTimeout(() => {
                    window.location.href = '/indexapp.html';
                }, 2000); // Delay for user to read toast
            } else {
                if (typeof toastr !== 'undefined') toastr.error(result.message || `Failed to save configuration: ${response.status}`);
            }
        } catch (error) {
            console.error('Save configuration error:', error);
            if (typeof toastr !== 'undefined') toastr.error('An unexpected error occurred while saving configuration.');
        } finally {
            setLoading(saveConfigurationButton, false, 'Save Configuration');
        }
    }

    function setLoading(buttonElement, isLoading, defaultText) {
        if (buttonElement) {
            if (isLoading) {
                buttonElement.disabled = true;
                buttonElement.innerHTML = `<i class="fas fa-spinner fa-spin"></i> ${defaultText.replace(/\.\.\.$/, 'ing...')}`;
            } else {
                buttonElement.disabled = false;
                buttonElement.innerHTML = defaultText;
            }
        }
    }
});
