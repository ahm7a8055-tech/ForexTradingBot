import React from 'react';
import { render, screen, act } from '@testing-library/react';
import { AuthProvider, useAuth } from './AuthContext'; // Adjust path as necessary
import '@testing-library/jest-dom';

// A helper component to consume and display context values
const TestConsumerComponent = () => {
  const { user, token, isAuthenticated, loading, login, logout } = useAuth();

  return (
    <div>
      <div data-testid="loading">{loading.toString()}</div>
      <div data-testid="isAuthenticated">{isAuthenticated.toString()}</div>
      {user && <div data-testid="username">{user.username}</div>}
      {token && <div data-testid="token">{token}</div>}
      <button onClick={async () => { try { await login('admin', 'admin'); } catch(e) { /* ignore in test consumer */ } }}>Login Admin</button>
      <button onClick={async () => { try { await login('wrong', 'user'); } catch(e) { /* ignore in test consumer */ } }}>Login Wrong</button>
      <button onClick={logout}>Logout</button>
    </div>
  );
};

// Helper to render with provider
const renderWithAuthProvider = (ui) => {
  return render(
    <AuthProvider>
      {ui}
    </AuthProvider>
  );
};

describe('AuthContext', () => {
  beforeEach(() => {
    // Clear localStorage before each test to ensure a clean state
    localStorage.clear();
  });

  test('initial state: not loading, not authenticated, no user/token', async () => {
    renderWithAuthProvider(<TestConsumerComponent />);

    // Wait for initial loading to complete (useEffect in AuthProvider)
    await act(async () => {
      await new Promise(resolve => setTimeout(resolve, 10)); // Small delay for useEffect
    });

    expect(screen.getByTestId('loading')).toHaveTextContent('false');
    expect(screen.getByTestId('isAuthenticated')).toHaveTextContent('false');
    expect(screen.queryByTestId('username')).not.toBeInTheDocument();
    expect(screen.queryByTestId('token')).not.toBeInTheDocument();
  });

  test('successful mock login updates context and localStorage', async () => {
    renderWithAuthProvider(<TestConsumerComponent />);

    // Ensure initial state is processed
    await act(async () => {
      await new Promise(resolve => setTimeout(resolve, 10));
    });

    const loginButton = screen.getByText('Login Admin');
    await act(async () => {
      loginButton.click();
      // mockAuthService.login has a 500ms timeout
      await new Promise(resolve => setTimeout(resolve, 600));
    });

    expect(screen.getByTestId('isAuthenticated')).toHaveTextContent('true');
    expect(screen.getByTestId('username')).toHaveTextContent('admin');
    expect(screen.getByTestId('token')).toHaveTextContent('fake-jwt-token');
    expect(localStorage.getItem('authToken')).toBe('fake-jwt-token');
  });

  test('failed mock login does not update context or localStorage', async () => {
    renderWithAuthProvider(<TestConsumerComponent />);
     await act(async () => {
      await new Promise(resolve => setTimeout(resolve, 10));
    });

    const loginWrongButton = screen.getByText('Login Wrong');

    // Suppress console.error for this specific test case if it logs expected errors
    const originalError = console.error;
    console.error = jest.fn();

    await act(async () => {
      loginWrongButton.click();
      // mockAuthService.login has a 500ms timeout for failure too
      await new Promise(resolve => setTimeout(resolve, 600));
    });

    console.error = originalError; // Restore console.error

    expect(screen.getByTestId('isAuthenticated')).toHaveTextContent('false');
    expect(screen.queryByTestId('username')).not.toBeInTheDocument();
    expect(screen.queryByTestId('token')).not.toBeInTheDocument();
    expect(localStorage.getItem('authToken')).toBeNull();
  });

  test('logout clears context and localStorage', async () => {
    renderWithAuthProvider(<TestConsumerComponent />);
    await act(async () => {
      await new Promise(resolve => setTimeout(resolve, 10)); // Initial load
    });

    // First, log in
    const loginButton = screen.getByText('Login Admin');
    await act(async () => {
      loginButton.click();
      await new Promise(resolve => setTimeout(resolve, 600)); // Login delay
    });

    expect(screen.getByTestId('isAuthenticated')).toHaveTextContent('true'); // Verify login

    // Then, log out
    const logoutButton = screen.getByText('Logout');
    await act(async () => {
      logoutButton.click();
      // mockAuthService.logout has a 200ms timeout
      await new Promise(resolve => setTimeout(resolve, 300));
    });

    expect(screen.getByTestId('isAuthenticated')).toHaveTextContent('false');
    expect(screen.queryByTestId('username')).not.toBeInTheDocument();
    expect(screen.queryByTestId('token')).not.toBeInTheDocument();
    expect(localStorage.getItem('authToken')).toBeNull();
  });

  test('restores session from localStorage if token is valid', async () => {
    localStorage.setItem('authToken', 'fake-jwt-token');

    renderWithAuthProvider(<TestConsumerComponent />);

    // useEffect in AuthProvider will try to restore session
    // mockAuthService.getCurrentUser has 200ms timeout
    await act(async () => {
      await new Promise(resolve => setTimeout(resolve, 300));
    });

    expect(screen.getByTestId('loading')).toHaveTextContent('false');
    expect(screen.getByTestId('isAuthenticated')).toHaveTextContent('true');
    expect(screen.getByTestId('username')).toHaveTextContent('admin');
    expect(screen.getByTestId('token')).toHaveTextContent('fake-jwt-token'); // Token is still from state, not directly from LS here
  });

   test('does not restore session if token in localStorage is invalid', async () => {
    localStorage.setItem('authToken', 'invalid-token');

    // Suppress console.error for this specific test case
    const originalError = console.error;
    console.error = jest.fn();

    renderWithAuthProvider(<TestConsumerComponent />);

    await act(async () => {
      await new Promise(resolve => setTimeout(resolve, 300)); // getCurrentUser delay
    });

    console.error = originalError; // Restore console.error

    expect(screen.getByTestId('loading')).toHaveTextContent('false');
    expect(screen.getByTestId('isAuthenticated')).toHaveTextContent('false');
    expect(screen.queryByTestId('username')).not.toBeInTheDocument();
    expect(localStorage.getItem('authToken')).toBeNull(); // AuthProvider should clear invalid token
  });
});
