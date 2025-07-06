import React from 'react';
import { Link } from 'react-router-dom'; // Import Link for navigation

const DashboardHomePage = () => {
  return (
    <div className="container mx-auto p-4">
      <h1 className="text-3xl font-bold mb-6 text-gray-800 dark:text-white">Welcome to the ForexTradingBot Admin Dashboard</h1>

      <div className="bg-white dark:bg-gray-800 shadow-md rounded-lg p-6 mb-8">
        <p className="text-lg text-gray-700 dark:text-gray-300 mb-4">
          This is your central hub for managing and monitoring the ForexTradingBot application.
          From here, you can configure critical settings, manage API connections, oversee user access (future),
          and ensure the system is running smoothly.
        </p>
        <p className="text-gray-600 dark:text-gray-400">
          Please use the navigation sidebar on the left to access different sections of the dashboard.
          Each section provides tools and information for specific aspects of the application.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {/* Quick Links Example - can be expanded */}
        <div className="bg-white dark:bg-gray-800 shadow-md rounded-lg p-6">
          <h2 className="text-xl font-semibold mb-3 text-gray-700 dark:text-gray-200">Quick Links</h2>
          <ul className="space-y-2">
            <li>
              <Link to="/app-settings" className="text-indigo-600 hover:text-indigo-800 dark:text-indigo-400 dark:hover:text-indigo-300 transition-colors">
                Application Settings
              </Link>
              <p className="text-xs text-gray-500 dark:text-gray-400">Manage Telegram bot configurations.</p>
            </li>
            <li>
              <Link to="/api-connections-management" className="text-indigo-600 hover:text-indigo-800 dark:text-indigo-400 dark:hover:text-indigo-300 transition-colors">
                API Connections
              </Link>
              <p className="text-xs text-gray-500 dark:text-gray-400">Configure Telegram client API ID/Hash.</p>
            </li>
            <li>
              <Link to="/system-settings" className="text-indigo-600 hover:text-indigo-800 dark:text-indigo-400 dark:hover:text-indigo-300 transition-colors">
                System Settings & Diagnostics
              </Link>
              <p className="text-xs text-gray-500 dark:text-gray-400">Check system health and manage application state.</p>
            </li>
          </ul>
        </div>

        <div className="bg-white dark:bg-gray-800 shadow-md rounded-lg p-6">
          <h2 className="text-xl font-semibold mb-3 text-gray-700 dark:text-gray-200">System Status Overview</h2>
          <p className="text-sm text-gray-600 dark:text-gray-400 mb-2">
            (Placeholder for a brief system status summary)
          </p>
          <ul className="space-y-1 text-sm">
             {/* This could later be populated by a quick API call */}
            <li className="flex items-center"><span className="mr-2 h-3 w-3 rounded-full bg-green-500"></span>Backend API: Operational</li>
            <li className="flex items-center"><span className="mr-2 h-3 w-3 rounded-full bg-yellow-500"></span>Telegram Bot: Connected (mock status)</li>
            <li className="flex items-center"><span className="mr-2 h-3 w-3 rounded-full bg-gray-400"></span>Background Tasks: Idle (mock status)</li>
          </ul>
           <p className="text-xs text-gray-500 dark:text-gray-400 mt-3">
            For detailed checks, please visit the <Link to="/system-settings" className="text-indigo-600 hover:underline dark:text-indigo-400">System Diagnostics</Link> page.
          </p>
        </div>

        {/* Add more info cards as needed */}
      </div>

      <div className="mt-8 p-6 bg-white dark:bg-gray-800 shadow-md rounded-lg">
        <h3 className="text-lg font-semibold mb-2 text-gray-700 dark:text-gray-200">Need Help?</h3>
        <p className="text-sm text-gray-600 dark:text-gray-400">
          If you encounter any issues or have questions about managing the application,
          please refer to the project documentation or contact the system administrator.
        </p>
      </div>

    </div>
  );
};

export default DashboardHomePage;
