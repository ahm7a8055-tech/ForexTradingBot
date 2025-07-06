import React from 'react';
import { BrowserRouter as Router, Routes, Route, Outlet } from 'react-router-dom';
import AppLayout from './components/layout/AppLayout';
import ProtectedRoute from './components/auth/ProtectedRoute';
import LoginPage from './pages/LoginPage';
import DashboardHomePage from './pages/DashboardHomePage';
import ApiConnectionsPage from './pages/ApiConnectionsPage';
import AppSettingsPage from './pages/AppSettingsPage';
import UserManagementPage from './pages/UserManagementPage';
import RoleManagementPage from './pages/RoleManagementPage';
import SystemSettingsPage from './pages/SystemSettingsPage'; // Import the new page
// Import other pages as they are created

// This component will be rendered by ProtectedRoute and will include the AppLayout
const LayoutWithOutlet = () => (
  <AppLayout>
    <Outlet /> {/* Child routes will render here */}
  </AppLayout>
);

function App() {
  return (
    <Router>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/*" // All other paths will be handled by this
          element={
            <ProtectedRoute>
              <LayoutWithOutlet />
            </ProtectedRoute>
          }
        >
          {/* Nested routes that will render inside AppLayout's <Outlet /> */}
          <Route index element={<DashboardHomePage />} /> {/* Default route for "/" after login */}
          <Route path="api-connections-management" element={<ApiConnectionsPage />} />
          <Route path="app-settings" element={<AppSettingsPage />} />
          <Route path="user-management" element={<UserManagementPage />} />
          <Route path="role-management" element={<RoleManagementPage />} />
          <Route path="system-settings" element={<SystemSettingsPage />} /> {/* Add route for SystemSettingsPage */}
          {/* Define other protected routes here, relative to the parent "/*" */}
          {/* Example for a 404 page (to be created and placed here)
          <Route path="*" element={<NotFoundPage />} />
          */}
        </Route>
      </Routes>
    </Router>
  );
}

export default App;
