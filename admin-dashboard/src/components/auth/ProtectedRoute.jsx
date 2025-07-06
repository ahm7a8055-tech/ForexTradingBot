import React from 'react';
import { useAuth } from '../../contexts/AuthContext'; // Adjusted path
import { Navigate, useLocation } from 'react-router-dom';
import FullPageSpinner from '../ui/FullPageSpinner'; // Adjusted path

const ProtectedRoute = ({ children }) => {
  const { isAuthenticated, loading } = useAuth(); // Renamed isLoading to loading to match AuthContext
  const location = useLocation();

  // 1. While verifying authentication status (initial load), show a loading indicator
  if (loading) {
    return <FullPageSpinner />;
  }

  // 2. If authenticated, render the requested component
  if (isAuthenticated) {
    return children;
  }

  // 3. If not authenticated (and no longer loading), redirect to the login page
  // We pass the original location in state so we can redirect back after login
  // This condition '&& !loading' is implicitly true if we fall through from the first 'if (loading)'
  return <Navigate to="/login" state={{ from: location }} replace />;
};

export default ProtectedRoute;
