import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter as Router } from 'react-router-dom'; // Needed for useNavigate, useLocation
import LoginPage from './LoginPage';
import { AuthProvider, useAuth } from '../contexts/AuthContext'; // To provide the context
import '@testing-library/jest-dom';

// Mock the useAuth hook
jest.mock('../contexts/AuthContext', () => ({
  ...jest.requireActual('../contexts/AuthContext'), // Import and retain default behavior
  useAuth: jest.fn(), // Mock useAuth specifically
}));

import { MemoryRouter, Routes, Route } from 'react-router-dom'; // Import MemoryRouter

// Mock only useNavigate, let MemoryRouter handle useLocation
const mockNavigate = jest.fn();
jest.mock('react-router-dom', () => ({
  ...jest.requireActual('react-router-dom'),
  useNavigate: () => mockNavigate,
}));

describe('LoginPage Component', () => {
  let mockLogin;

  beforeEach(() => {
    // Reset mocks before each test
    mockLogin = jest.fn();
    useAuth.mockReturnValue({ // useAuth is already mocked at the top level
      login: mockLogin,
      user: null,
      isAuthenticated: false,
      loading: false,
    });
    mockNavigate.mockClear();
    localStorage.clear();
  });

  // Helper to render with MemoryRouter and specific initial state
  const renderWithMemoryRouter = (ui, initialPath = '/login', initialLocationState = null) => {
    return render(
      <MemoryRouter initialEntries={[{ pathname: initialPath, state: initialLocationState }]}>
        <Routes>
          <Route path="/login" element={ui} />
          {/* Dummy routes for navigation targets */}
          <Route path="/intended" element={<div>Intended Page Content</div>} />
          <Route path="/" element={<div>Dashboard Home Content</div>} />
        </Routes>
      </MemoryRouter>
    );
  };

  test('renders login form correctly', () => {
    renderWithMemoryRouter(<LoginPage />);
    expect(screen.getByText(/ForexSignalBot/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Username/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Password/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Access Dashboard/i })).toBeInTheDocument();
  });

  test('allows typing in username and password fields', () => {
    renderWithMemoryRouter(<LoginPage />);
    const usernameInput = screen.getByLabelText(/Username/i);
    const passwordInput = screen.getByLabelText(/Password/i);

    fireEvent.change(usernameInput, { target: { value: 'testuser' } });
    fireEvent.change(passwordInput, { target: { value: 'password123' } });

    expect(usernameInput.value).toBe('testuser');
    expect(passwordInput.value).toBe('password123');
  });

  test('calls login and navigates on successful submission to intended path', async () => {
    mockLogin.mockResolvedValueOnce(); // Simulate successful login

    // Pass the 'from' state via MemoryRouter's initialEntries
    renderWithMemoryRouter(<LoginPage />, '/login', { from: { pathname: '/intended' } });

    fireEvent.change(screen.getByLabelText(/Username/i), { target: { value: 'admin' } });
    fireEvent.change(screen.getByLabelText(/Password/i), { target: { value: 'admin' } });
    fireEvent.click(screen.getByRole('button', { name: /Access Dashboard/i }));

    await waitFor(() => {
      expect(mockLogin).toHaveBeenCalledWith('admin', 'admin');
    });
    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/intended', { replace: true });
    });
  });

  test('navigates to root on successful submission if no "from" location', async () => {
    mockLogin.mockResolvedValueOnce(); // Simulate successful login

    // initialLocationState defaults to null, which means 'from' will be '/'
    renderWithMemoryRouter(<LoginPage />);

    fireEvent.change(screen.getByLabelText(/Username/i), { target: { value: 'admin' } });
    fireEvent.change(screen.getByLabelText(/Password/i), { target: { value: 'admin' } });
    fireEvent.click(screen.getByRole('button', { name: /Access Dashboard/i }));

    await waitFor(() => {
      expect(mockLogin).toHaveBeenCalledWith('admin', 'admin');
    });
    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/', { replace: true });
    });
  });

  test('displays error message on failed login', async () => {
    mockLogin.mockRejectedValueOnce(new Error('Invalid credentials'));

    renderWithMemoryRouter(<LoginPage />);

    fireEvent.change(screen.getByLabelText(/Username/i), { target: { value: 'wronguser' } });
    fireEvent.change(screen.getByLabelText(/Password/i), { target: { value: 'wrongpass' } });
    fireEvent.click(screen.getByRole('button', { name: /Access Dashboard/i }));

    await waitFor(() => {
      expect(mockLogin).toHaveBeenCalledWith('wronguser', 'wrongpass');
    });
    expect(await screen.findByText('Invalid credentials')).toBeInTheDocument();
  });

  test('login button is disabled and shows processing text during login attempt', async () => {
    // Make login promise never resolve to keep it in loading state for a bit
    mockLogin.mockImplementation(() => new Promise(() => {}));

    renderWithMemoryRouter(<LoginPage />);

    fireEvent.change(screen.getByLabelText(/Username/i), { target: { value: 'admin' } });
    fireEvent.change(screen.getByLabelText(/Password/i), { target: { value: 'admin' } });
    fireEvent.click(screen.getByRole('button', { name: /Access Dashboard/i }));

    await waitFor(() => {
      const button = screen.getByRole('button', { name: /Processing.../i });
      expect(button).toBeDisabled();
      expect(screen.getByText(/Processing.../i)).toBeInTheDocument();
    });
  });
});
