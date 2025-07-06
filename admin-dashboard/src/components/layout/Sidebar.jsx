import React from 'react';
import { Link } from 'react-router-dom';

const Sidebar = () => {
  return (
    <aside className="w-64 bg-gray-700 text-white p-4 space-y-2">
      <nav>
        <ul>
          <li><Link to="/" className="block py-2 px-3 hover:bg-gray-600 rounded">Dashboard Home</Link></li>
          <li><Link to="/api-connections-management" className="block py-2 px-3 hover:bg-gray-600 rounded">API Connections</Link></li>
          <li><Link to="/app-settings" className="block py-2 px-3 hover:bg-gray-600 rounded">App Settings</Link></li>
          <li><Link to="/user-management" className="block py-2 px-3 hover:bg-gray-600 rounded">User Management</Link></li>
          <li><Link to="/role-management" className="block py-2 px-3 hover:bg-gray-600 rounded">Role Management</Link></li>
          <li><Link to="/system-settings" className="block py-2 px-3 hover:bg-gray-600 rounded">System Settings</Link></li>
          {/* More links will be added as features are implemented */}
        </ul>
      </nav>
    </aside>
  );
};

export default Sidebar;
