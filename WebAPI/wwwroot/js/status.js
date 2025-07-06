document.addEventListener('DOMContentLoaded', () => {
    const loadingOverlay = document.getElementById('loading-overlay-status');

    // Connectivity Status Elements
    const dbStatusElem = document.getElementById('dbStatus');
    const dbErrorElem = document.getElementById('dbError');
    const telegramApiStatusElem = document.getElementById('telegramApiStatus');
    const telegramApiErrorElem = document.getElementById('telegramApiError');

    // Hangfire Status Elements
    const hfServerCountElem = document.getElementById('hfServerCount');
    const hfEnqueuedCountElem = document.getElementById('hfEnqueuedCount');
    const hfScheduledCountElem = document.getElementById('hfScheduledCount');
    const hfProcessingCountElem = document.getElementById('hfProcessingCount');
    const hfSucceededCountElem = document.getElementById('hfSucceededCount');
    const hfFailedCountElem = document.getElementById('hfFailedCount');
    const hfQueuesContainerElem = document.getElementById('hfQueuesContainer');
    const hfServersContainerElem = document.getElementById('hfServersContainer');
    const hfRecurringJobCountElem = document.getElementById('hfRecurringJobCount');
    const hfRecurringJobsContainerElem = document.getElementById('hfRecurringJobsContainer');

    async function fetchAllStatusData() {
        if (loadingOverlay) loadingOverlay.style.display = 'flex';

        await fetchConnectivityStatus();
        await fetchHangfireStatus();

        if (loadingOverlay) loadingOverlay.style.display = 'none';
    }

    async function fetchConnectivityStatus() {
        try {
            const response = await fetch('/api/diagnostics/connectivity-status');
            if (!response.ok) {
                if (response.status === 401) window.location.href = '/login.html';
                throw new Error(`Failed to fetch connectivity status: ${response.status}`);
            }
            const data = await response.json();
            updateConnectivityUI(data);
        } catch (error) {
            console.error('Error fetching connectivity status:', error);
            if (typeof toastr !== 'undefined') toastr.error('Could not load connectivity status.');
            if (dbStatusElem) dbStatusElem.textContent = 'Error';
            if (dbStatusElem) dbStatusElem.className = 'status-error fw-bold';
            if (telegramApiStatusElem) telegramApiStatusElem.textContent = 'Error';
            if (telegramApiStatusElem) telegramApiStatusElem.className = 'status-error fw-bold';
        }
    }

    function updateConnectivityUI(data) {
        if (dbStatusElem) {
            dbStatusElem.textContent = data.canConnectToDatabase ? 'OK' : 'Error';
            dbStatusElem.className = data.canConnectToDatabase ? 'status-ok fw-bold' : 'status-error fw-bold';
        }
        if (dbErrorElem) {
            dbErrorElem.textContent = data.databaseError ? `(${data.databaseError})` : '';
            if (data.databaseError) dbErrorElem.classList.add('text-danger', 'small');
        }

        if (telegramApiStatusElem) {
            telegramApiStatusElem.textContent = data.canAccessTelegramApi ? `OK (Bot: ${data.telegramBotUsername || 'Unknown'})` : 'Error';
            telegramApiStatusElem.className = data.canAccessTelegramApi ? 'status-ok fw-bold' : 'status-error fw-bold';
        }
        if (telegramApiErrorElem) {
            telegramApiErrorElem.textContent = data.telegramApiError ? `(${data.telegramApiError})` : '';
            if (data.telegramApiError) telegramApiErrorElem.classList.add('text-danger', 'small');
        }
    }

    async function fetchHangfireStatus() {
        try {
            const response = await fetch('/api/diagnostics/hangfire-status');
            if (!response.ok) {
                if (response.status === 401) window.location.href = '/login.html';
                throw new Error(`Failed to fetch Hangfire status: ${response.status}`);
            }
            const data = await response.json();
            updateHangfireUI(data);
        } catch (error) {
            console.error('Error fetching Hangfire status:', error);
            if (typeof toastr !== 'undefined') toastr.error('Could not load Hangfire status.');
            // Could set text to 'Error' for Hangfire elements here
        }
    }

    function updateHangfireUI(data) {
        if (hfServerCountElem) hfServerCountElem.textContent = data.serverCount;
        if (hfEnqueuedCountElem) hfEnqueuedCountElem.textContent = data.enqueuedCount;
        if (hfScheduledCountElem) hfScheduledCountElem.textContent = data.scheduledCount;
        if (hfProcessingCountElem) hfProcessingCountElem.textContent = data.processingCount;
        if (hfSucceededCountElem) hfSucceededCountElem.textContent = data.succeededCount;
        if (hfFailedCountElem) hfFailedCountElem.textContent = data.failedCount;

        if (hfQueuesContainerElem) {
            if (data.queues && data.queues.length > 0) {
                let queuesHtml = '<ul class="list-group">';
                data.queues.forEach(q => {
                    queuesHtml += `<li class="list-group-item d-flex justify-content-between align-items-center">
                                    ${q.name}
                                    <span class="badge bg-primary rounded-pill">Length: ${q.length}</span>
                                    <span class="badge bg-secondary rounded-pill">Fetched: ${q.fetched}</span>
                                   </li>`;
                });
                queuesHtml += '</ul>';
                hfQueuesContainerElem.innerHTML = queuesHtml;
            } else {
                hfQueuesContainerElem.innerHTML = '<p>No active queues found or reported.</p>';
            }
        }

        if (hfServersContainerElem) {
             if (data.servers && data.servers.length > 0) {
                let serversHtml = '<ul class="list-group">';
                data.servers.forEach(s => {
                    serversHtml += `<li class="list-group-item small">${s}</li>`;
                });
                serversHtml += '</ul>';
                hfServersContainerElem.innerHTML = serversHtml;
            } else {
                hfServersContainerElem.innerHTML = '<p>No active Hangfire servers reported.</p>';
            }
        }


        if (hfRecurringJobCountElem) hfRecurringJobCountElem.textContent = data.recurringJobs ? data.recurringJobs.length : 0;
        if (hfRecurringJobsContainerElem) {
            if (data.recurringJobs && data.recurringJobs.length > 0) {
                let tableHtml = `<table class="table table-sm table-hover recurring-job-table">
                                    <thead>
                                        <tr>
                                            <th>ID</th>
                                            <th>Method</th>
                                            <th>Cron</th>
                                            <th>Queue</th>
                                            <th>Next Run</th>
                                            <th>Last Run</th>
                                            <th>Status</th>
                                        </tr>
                                    </thead>
                                    <tbody>`;
                data.recurringJobs.forEach(job => {
                    let status = job.error ? `<span class="text-danger" title="${job.error}"><i class="fas fa-exclamation-circle"></i> Error</span>` : '<span class="text-success"><i class="fas fa-check-circle"></i> OK</span>';
                    if (job.removed) status = '<span class="text-muted"><i class="fas fa-trash"></i> Removed</span>';

                    tableHtml += `<tr>
                                    <td>${job.id}</td>
                                    <td title="${job.method}"><small>${job.method.split('.').pop()}</small></td>
                                    <td><code>${job.cron}</code></td>
                                    <td>${job.queue || 'default'}</td>
                                    <td>${job.nextExecution ? new Date(job.nextExecution).toLocaleString() : 'N/A'}</td>
                                    <td>${job.lastExecution ? new Date(job.lastExecution).toLocaleString() : 'N/A'}</td>
                                    <td>${status}</td>
                                  </tr>`;
                });
                tableHtml += '</tbody></table>';
                hfRecurringJobsContainerElem.innerHTML = tableHtml;
            } else {
                hfRecurringJobsContainerElem.innerHTML = '<p>No recurring jobs found.</p>';
            }
        }
    }

    // Initial data fetch
    fetchAllStatusData();
});
