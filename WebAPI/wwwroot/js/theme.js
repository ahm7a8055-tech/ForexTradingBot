document.addEventListener('DOMContentLoaded', () => {
    const themeToggleButton = document.getElementById('themeToggleButton');
    const currentTheme = localStorage.getItem('theme') ? localStorage.getItem('theme') : null;
    const prefersDarkScheme = window.matchMedia('(prefers-color-scheme: dark)');

    function applyTheme(theme) {
        if (theme === 'dark') {
            document.body.classList.add('dark-mode');
            if (themeToggleButton) themeToggleButton.innerHTML = '<i class="fas fa-sun"></i> Light Mode';
        } else {
            document.body.classList.remove('dark-mode');
            if (themeToggleButton) themeToggleButton.innerHTML = '<i class="fas fa-moon"></i> Dark Mode';
        }
    }

    if (currentTheme) {
        applyTheme(currentTheme);
    } else if (prefersDarkScheme.matches) {
        applyTheme('dark'); // Default to system preference if no explicit choice saved
        localStorage.setItem('theme', 'dark'); // Save this implicit choice
    } else {
        applyTheme('light'); // Default to light if no preference and system is not dark
    }

    if (themeToggleButton) {
        themeToggleButton.addEventListener('click', () => {
            let newTheme;
            if (document.body.classList.contains('dark-mode')) {
                newTheme = 'light';
            } else {
                newTheme = 'dark';
            }
            applyTheme(newTheme);
            localStorage.setItem('theme', newTheme);
        });
    }
});
