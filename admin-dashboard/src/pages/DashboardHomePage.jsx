import React from 'react';
import { Link } from 'react-router-dom';

export default function DashboardHomePage() {
  return (
    <div className="min-h-screen bg-gray-100 flex">
      {/* Sidebar */}
      <aside className="w-64 bg-white shadow-lg hidden md:flex flex-col p-6 space-y-6">
        <div className="text-2xl font-bold text-blue-700 mb-8">ForexTradingBot</div>
        <nav className="flex-1">
          <ul className="space-y-4">
            <li><Link to="/" className="text-gray-700 hover:text-blue-600 font-medium transition-colors">Dashboard Home</Link></li>
            <li><Link to="/api-connections-management" className="text-gray-700 hover:text-blue-600 font-medium transition-colors">API Connections</Link></li>
            <li><Link to="/app-settings" className="text-gray-700 hover:text-blue-600 font-medium transition-colors">App Settings</Link></li>
            <li><Link to="/user-management" className="text-gray-700 hover:text-blue-600 font-medium transition-colors">User Management</Link></li>
            <li><Link to="/role-management" className="text-gray-700 hover:text-blue-600 font-medium transition-colors">Role Management</Link></li>
            <li><Link to="/system-settings" className="text-gray-700 hover:text-blue-600 font-medium transition-colors">System Settings</Link></li>
          </ul>
        </nav>
        <div className="mt-auto text-xs text-gray-400">© 2025 ForexTradingBot</div>
      </aside>
      {/* Main Content */}
      <div className="flex-1 flex flex-col">
        {/* Header */}
        <header className="bg-white shadow flex items-center justify-between px-8 py-4">
          <h1 className="text-2xl font-bold text-gray-800">Admin Dashboard</h1>
          <button className="flex items-center space-x-2 bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 transition-colors">
            <span className="material-icons">logout</span>
            <span>Logout</span>
          </button>
        </header>
        {/* Dashboard Content */}
        <main className="flex-1 p-8 overflow-y-auto">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8 mb-8">
            {/* Card 1 */}
            <div className="bg-white rounded-xl shadow p-6 flex flex-col items-start">
              <h2 className="text-lg font-semibold text-blue-700 mb-2">Quick Links</h2>
              <ul className="space-y-2">
                <li><Link to="/app-settings" className="text-blue-600 hover:underline">Application Settings</Link></li>
                <li><Link to="/api-connections-management" className="text-blue-600 hover:underline">API Connections</Link></li>
                <li><Link to="/system-settings" className="text-blue-600 hover:underline">System Settings & Diagnostics</Link></li>
              </ul>
            </div>
            {/* Card 2 */}
            <div className="bg-white rounded-xl shadow p-6 flex flex-col items-start">
              <h2 className="text-lg font-semibold text-blue-700 mb-2">System Status Overview</h2>
              <ul className="text-sm text-gray-700 space-y-1">
                <li><span className="inline-block w-3 h-3 rounded-full bg-green-500 mr-2"></span>Backend API: <span className="font-medium">Operational</span></li>
                <li><span className="inline-block w-3 h-3 rounded-full bg-yellow-500 mr-2"></span>Telegram Bot: <span className="font-medium">Connected</span> (mock status)</li>
                <li><span className="inline-block w-3 h-3 rounded-full bg-gray-400 mr-2"></span>Background Tasks: <span className="font-medium">Idle</span> (mock status)</li>
              </ul>
              <p className="text-xs text-gray-500 mt-3">For detailed checks, visit the <Link to="/system-settings" className="text-blue-600 hover:underline">System Diagnostics</Link> page.</p>
            </div>
            {/* Card 3 */}
            <div className="bg-white rounded-xl shadow p-6 flex flex-col items-start">
              <h2 className="text-lg font-semibold text-blue-700 mb-2">Need Help?</h2>
              <p className="text-sm text-gray-700 mb-2">If you encounter any issues or have questions about managing the application, please refer to the project documentation or contact the system administrator.</p>
            </div>
          </div>
          {/* Welcome Section */}
          <div className="bg-gradient-to-r from-blue-50 to-purple-50 rounded-xl shadow p-8">
            <h2 className="text-2xl font-bold text-gray-800 mb-2">Welcome to the ForexTradingBot Admin Dashboard</h2>
            <p className="text-gray-700 mb-2">This is your central hub for managing and monitoring the ForexTradingBot application. From here, you can configure critical settings, manage API connections, oversee user access (future), and ensure the system is running smoothly.</p>
            <p className="text-gray-500">Use the navigation sidebar to access different sections. Each section provides tools and information for specific aspects of the application.</p>
          </div>
        </main>
      </div>
    </div>
  );
}
