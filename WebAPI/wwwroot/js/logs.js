document.addEventListener('DOMContentLoaded', () => {
    const loadingOverlay = document.getElementById('loading-overlay-logs');
    const logFileSelect = document.getElementById('logFileSelect');
    const lineCountInput = document.getElementById('lineCountInput');
    const viewLogButton = document.getElementById('viewLogButton');
    const downloadZipButton = document.getElementById('downloadZipButton');
    const logContentContainer = document.getElementById('logContentContainer');
    const currentLogFileNameElem = document.getElementById('currentLogFileName');

    async function fetchLogFiles() {
        if (loadingOverlay) loadingOverlay.style.display = 'flex';
        try {
            const response = await fetch('/api/logs/list');
            if (!response.ok) {
                if (response.status === 401) window.location.href = '/login.html';
                throw new Error(`Failed to fetch log files list: ${response.status}`);
            }
            const files = await response.json();
            populateLogFileSelect(files);
        } catch (error) {
            console.error('Error fetching log files list:', error);
            if (typeof toastr !== 'undefined') toastr.error('Could not load log files list.');
            if (logFileSelect) logFileSelect.innerHTML = '<option value="">Error loading logs</option>';
        } finally {
            if (loadingOverlay) loadingOverlay.style.display = 'none';
        }
    }

    function populateLogFileSelect(files) {
        if (!logFileSelect) return;
        logFileSelect.innerHTML = '<option value="">-- Select a log file --</option>';
        if (files && files.length > 0) {
            files.forEach(file => {
                const option = document.createElement('option');
                option.value = file;
                option.textContent = file;
                logFileSelect.appendChild(option);
            });
        } else {
            logFileSelect.innerHTML = '<option value="">No log files found</option>';
        }
    }

    async function viewLogContent() {
        if (!logFileSelect || !logContentContainer || !currentLogFileNameElem) return;

        const selectedFile = logFileSelect.value;
        const lines = parseInt(lineCountInput.value, 10) || 0; // 0 for all lines

        if (!selectedFile) {
            if (typeof toastr !== 'undefined') toastr.warning('Please select a log file to view.');
            return;
        }

        if (loadingOverlay) loadingOverlay.style.display = 'flex';
        logContentContainer.textContent = 'Loading log content...';
        currentLogFileNameElem.textContent = `Viewing: ${selectedFile}`;

        try {
            let apiUrl = `/api/logs/view/${encodeURIComponent(selectedFile)}`;
            if (lines > 0) {
                apiUrl += `?lineCount=${lines}`;
            }

            const response = await fetch(apiUrl);
            if (!response.ok) {
                if (response.status === 401) window.location.href = '/login.html';
                if (response.status === 404) throw new Error(`Log file "${selectedFile}" not found.`);
                throw new Error(`Failed to fetch log content: ${response.status}`);
            }
            const content = await response.text();
            logContentContainer.textContent = content || '(Log file is empty or content could not be displayed)';
        } catch (error) {
            console.error('Error viewing log content:', error);
            if (typeof toastr !== 'undefined') toastr.error(`Could not load log content: ${error.message}`);
            logContentContainer.textContent = `Error loading log: ${error.message}`;
        } finally {
            if (loadingOverlay) loadingOverlay.style.display = 'none';
        }
    }

    if (viewLogButton) {
        viewLogButton.addEventListener('click', viewLogContent);
    }

    if (downloadZipButton) {
        downloadZipButton.addEventListener('click', () => {
            // This will trigger a file download through the browser
            window.location.href = '/api/logs/zip';
        });
    }

    // Initial load
    fetchLogFiles();
});
