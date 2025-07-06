import React from 'react';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import ProtectedRoute from './ProtectedRoute'; // Adjust path
import FullPageSpinner from '../ui/FullPageSpinner'; // Adjust path
import { useAuth } from '../../contexts/AuthContext'; // Adjust path
import '@testing-library/jest-dom';

// Mock useAuth hook
jest.mock('../../contexts/AuthContext', () => ({
  useAuth: jest.fn(),
}));

// Mock FullPageSpinner to simplify testing its presence
jest.mock('../ui/FullPageSpinner', () => () => <div data-testid="full-page-spinner">Loading...</div>);

// Helper component to display current location for redirection testing
const LocationDisplay = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}{location.search}{location.state ? JSON.stringify(location.state) : ''}</div>;
};

const TestProtectedContent = () => <div data-testid="protected-content">Protected Content</div>;

describe('ProtectedRoute Component', () => {
  const renderWithRouterAndAuth = (authValue, initialRoute = '/protected') => {
    useAuth.mockReturnValue(authValue);

    return render(
      <MemoryRouter initialEntries={[initialRoute]}>
        <Routes>
          <Route
            path="/protected"
            element={
              <ProtectedRoute>
                <TestProtectedContent />
              </ProtectedRoute>
            }
          />
          <Route path="/login" element={<LocationDisplay />} />
        </Routes>
      </MemoryRouter>
    );
  };

  test('renders children when authenticated and not loading', () => {
    renderWithRouterAndAuth({ isAuthenticated: true, loading: false });
    expect(screen.getByTestId('protected-content')).toBeInTheDocument();
    expect(screen.queryByTestId('full-page-spinner')).not.toBeInTheDocument();
    expect(screen.queryByTestId('location-display')).not.toBeInTheDocument(); // Should not redirect
  });

  test('redirects to /login when not authenticated and not loading, passing state', () => {
    const initialRoute = '/protected?param=123';
    renderWithRouterAndAuth({ isAuthenticated: false, loading: false }, initialRoute);

    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
    expect(screen.queryByTestId('full-page-spinner')).not.toBeInTheDocument();

    const locationDisplay = screen.getByTestId('location-display');
    expect(locationDisplay).toBeInTheDocument();
    expect(locationDisplay).toHaveTextContent('/login');
    // Check that the 'from' state was passed correctly
    // The state object will be { from: { pathname: '/protected', search: '?param=123', hash: '', state: null, key: 'default' } }
    // We need to parse the JSON string from the text content.
    const locationStateText = locationDisplay.textContent.replace('/login', '');
    const locationState = JSON.parse(locationStateText);
    expect(locationState.from.pathname).toBe('/protected');
    expect(locationState.from.search).toBe('?param=123');
  });

  test('shows FullPageSpinner when loading', () => {
    renderWithRouterAndAuth({ isAuthenticated: false, loading: true }); // Auth status doesn't matter if loading
    expect(screen.getByTestId('full-page-spinner')).toBeInTheDocument();
    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
    expect(screen.queryByTestId('location-display')).not.toBeInTheDocument();
  });

  test('shows FullPageSpinner when loading even if authenticated', () => {
    renderWithRouterAndAuth({ isAuthenticated: true, loading: true });
    expect(screen.getByTestId('full-page-spinner')).toBeInTheDocument();
    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
    expect(screen.queryByTestId('location-display')).not.toBeInTheDocument();
  });
});
