import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import Header from './Header'; // Adjust path
import { useAuth } from '../../contexts/AuthContext'; // Adjust path
import { useTheme } from '../../contexts/ThemeContext'; // Adjust path
import '@testing-library/jest-dom';

// Mock useAuth
jest.mock('../../contexts/AuthContext', () => ({
  useAuth: jest.fn(),
}));

// Mock useTheme
jest.mock('../../contexts/ThemeContext', () => ({
  useTheme: jest.fn(),
}));

// Mock useNavigate from react-router-dom
const mockNavigate = jest.fn();
jest.mock('react-router-dom', () => ({
  ...jest.requireActual('react-router-dom'), // Import and retain default behavior
  useNavigate: () => mockNavigate,
}));


describe('Header Component', () => {
  let mockLogout;
  let mockToggleTheme;

  const setup = (authValue, themeValue) => {
    mockLogout = jest.fn().mockResolvedValue(undefined); // Ensure logout is async if AuthContext expects
    mockToggleTheme = jest.fn();

    useAuth.mockReturnValue({
      user: authValue.user, // Can be null or a user object
      logout: mockLogout,
      // Add other properties if Header uses them, e.g. isAuthenticated, loading
    });
    useTheme.mockReturnValue({
      theme: themeValue.theme, // 'light' or 'dark'
      toggleTheme: mockToggleTheme,
    });
    mockNavigate.mockClear();

    // Wrap in MemoryRouter because Header might contain Link components or use router hooks indirectly
    // Even if not directly, child components (like a user dropdown) might.
    // For this specific Header, useNavigate is directly used for logout.
    render(
      <MemoryRouter>
        <Header />
      </MemoryRouter>
    );
  };

  test('renders Admin Dashboard title and theme toggle button', () => {
    setup({ user: null }, { theme: 'light' });
    expect(screen.getByText('Admin Dashboard')).toBeInTheDocument();
    expect(screen.getByLabelText('Toggle theme')).toBeInTheDocument();
  });

  test('theme toggle button calls toggleTheme when clicked', () => {
    setup({ user: null }, { theme: 'light' });
    const themeToggleButton = screen.getByLabelText('Toggle theme');
    fireEvent.click(themeToggleButton);
    expect(mockToggleTheme).toHaveBeenCalledTimes(1);
  });

  test('does not render logout button if user is not authenticated', () => {
    setup({ user: null }, { theme: 'light' }); // No user
    expect(screen.queryByRole('button', { name: /Logout/i })).not.toBeInTheDocument();
  });

  test('renders logout button if user is authenticated', () => {
    setup({ user: { username: 'admin' } }, { theme: 'light' }); // User is present
    expect(screen.getByRole('button', { name: /Logout/i })).toBeInTheDocument();
  });

  test('logout button calls logout and navigates to /login when clicked', async () => {
    setup({ user: { username: 'admin' } }, { theme: 'light' });

    const logoutButton = screen.getByRole('button', { name: /Logout/i });
    fireEvent.click(logoutButton);

    // Check that logout from AuthContext was called
    await waitFor(() => expect(mockLogout).toHaveBeenCalledTimes(1));

    // Check that navigate was called to redirect to /login
    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith('/login'));
  });

  test('displays moon icon when theme is light', () => {
    setup({ user: null }, { theme: 'light' });
    const themeButton = screen.getByLabelText('Toggle theme');
    // Moon icon path
    expect(themeButton.querySelector('svg path[d^="M20.354"]')).toBeInTheDocument();
    // Sun icon path should not be present
    expect(themeButton.querySelector('svg path[d^="M12 3v1m0"]')).not.toBeInTheDocument();
  });

  test('displays sun icon when theme is dark', () => {
    setup({ user: null }, { theme: 'dark' });
    const themeButton = screen.getByLabelText('Toggle theme');
    // Sun icon path
    expect(themeButton.querySelector('svg path[d^="M12 3v1m0"]')).toBeInTheDocument();
    // Moon icon path should not be present
    expect(themeButton.querySelector('svg path[d^="M20.354"]')).not.toBeInTheDocument();
  });
});
