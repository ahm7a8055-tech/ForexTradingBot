document.addEventListener('DOMContentLoaded', () => {
    const logoutButton = document.getElementById('logoutButton');
    const goToConfigLink = document.getElementById('goToConfigLink'); // Assuming a link/button with this ID
    const loadingOverlay = document.getElementById('loading-overlay');

    const totalUsersElem = document.getElementById('totalUsers');
    const signalsTodayElem = document.getElementById('signalsToday');
    // const messagesTodayElem = document.getElementById('messagesToday'); // Deferred

    if (logoutButton) {
        logoutButton.addEventListener('click', async () => {
            try {
                const response = await fetch('/api/auth/logout', { method: 'POST' });
                if (response.ok) {
                    if (typeof toastr !== 'undefined') {
                        toastr.success('Logout successful!');
                    }
                    window.location.href = '/login.html';
                } else {
                    if (typeof toastr !== 'undefined') {
                        toastr.error('Logout failed. Please try again.');
                    }
                }
            } catch (error) {
                console.error('Logout error:', error);
                if (typeof toastr !== 'undefined') {
                    toastr.error('An error occurred during logout.');
                }
            }
        });
    }

    if (goToConfigLink) {
        goToConfigLink.addEventListener('click', (e) => {
            e.preventDefault();
            window.location.href = '/config.html';
        });
    }

    async function fetchDashboardData() {
        if(loadingOverlay) loadingOverlay.style.display = 'flex';
        try {
            const response = await fetch('/api/admin/stats');
            if (!response.ok) {
                if (response.status === 401) { // Unauthorized
                    if (typeof toastr !== 'undefined') toastr.error('Session expired. Please login again.');
                    window.location.href = '/login.html';
                } else {
                    throw new Error(`Failed to fetch stats: ${response.status}`);
                }
                return;
            }
            const data = await response.json();
            populateDashboard(data);
        } catch (error) {
            console.error('Failed to fetch dashboard data:', error);
            if (typeof toastr !== 'undefined') {
                toastr.error('Could not load dashboard data. Please try again or re-login.');
            }
            // Potentially redirect to login if it's an auth issue that wasn't a 401
            // window.location.href = '/login.html';
        } finally {
            if(loadingOverlay) loadingOverlay.style.display = 'none';
        }
    }

    function populateDashboard(data) {
        if (totalUsersElem) totalUsersElem.textContent = data.totalUsers ?? 'N/A';
        if (signalsTodayElem) signalsTodayElem.textContent = data.signalsToday ?? 'N/A';
        // if (messagesTodayElem) messagesTodayElem.textContent = data.messagesToday ?? 'N/A'; // Deferred

        renderUserGrowthChart(data.userGrowthLast7Days || []);
        renderSignalsPerDayChart(data.signalsPerDayLast7Days || []);
    }

    function renderUserGrowthChart(userGrowthData) {
        const ctx = document.getElementById('userGrowthChart');
        if (!ctx) return;

        const labels = userGrowthData.map(d => new Date(d.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }));
        const counts = userGrowthData.map(d => d.count);

        new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'New Users',
                    data: counts,
                    backgroundColor: 'rgba(54, 162, 235, 0.6)',
                    borderColor: 'rgba(54, 162, 235, 1)',
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            stepSize: 1 // Ensure y-axis shows whole numbers for user counts
                        }
                    }
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                    }
                }
            }
        });
    }

    function renderSignalsPerDayChart(signalsData) {
        const ctx = document.getElementById('signalsPerDayChart');
        if (!ctx) return;

        const labels = signalsData.map(d => new Date(d.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }));
        const counts = signalsData.map(d => d.count);

        new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Signals Sent',
                    data: counts,
                    fill: false,
                    borderColor: 'rgba(75, 192, 192, 1)',
                    tension: 0.1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                    }
                }
            }
        });
    }

    // Initial data fetch
    fetchDashboardData();
});
