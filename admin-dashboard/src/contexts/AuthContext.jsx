import React, { createContext, useState, useContext, useEffect } from 'react';

const AuthContext = createContext(null);

export const useAuth = () => useContext(AuthContext);

// Mock auth service for now
const mockAuthService = {
  login: async (username, password) => {
    return new Promise((resolve, reject) => {
      setTimeout(() => {
        if (username === 'admin' && password === 'admin') {
          const mockUser = { id: '1', username: 'admin', name: 'Administrator' };
          const mockToken = 'fake-jwt-token'; // Simulate a JWT token
          resolve({ user: mockUser, token: mockToken });
        } else {
          reject(new Error('Invalid credentials'));
        }
      }, 500);
    });
  },
  logout: async () => {
    return new Promise((resolve) => {
      setTimeout(() => {
        resolve();
      }, 200);
    });
  },
  // Simulate checking token validity or fetching user profile
  getCurrentUser: async (token) => {
    return new Promise((resolve, reject) => {
      setTimeout(() => {
        if (token === 'fake-jwt-token') {
          resolve({ id: '1', username: 'admin', name: 'Administrator' });
        } else {
          reject(new Error('Invalid or expired token'));
        }
      }, 200);
    });
  }
};

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(localStorage.getItem('authToken'));
  const [loading, setLoading] = useState(true); // To track initial auth state check

  useEffect(() => {
    const initializeAuth = async () => {
      if (token) {
        try {
          // In a real app, you'd validate the token with the backend here
          // For now, we use a mock validation or user fetch
          const currentUser = await mockAuthService.getCurrentUser(token);
          setUser(currentUser);
          localStorage.setItem('authToken', token); // Refresh/confirm token storage
        } catch (error) {
          console.error("Session restore failed:", error);
          localStorage.removeItem('authToken');
          setToken(null);
          setUser(null);
        }
      }
      setLoading(false);
    };
    initializeAuth();
  }, [token]);

  const login = async (username, password) => {
    try {
      const { user: loggedInUser, token: newAuthToken } = await mockAuthService.login(username, password);
      setUser(loggedInUser);
      setToken(newAuthToken);
      localStorage.setItem('authToken', newAuthToken);
    } catch (error) {
      console.error("Login failed:", error);
      // Explicitly clear token state and localStorage on failed login attempt
      setUser(null);
      setToken(null);
      localStorage.removeItem('authToken');
      throw error; // Re-throw to be caught by the login form
    }
  };

  const logout = async () => {
    await mockAuthService.logout();
    setUser(null);
    setToken(null);
    localStorage.removeItem('authToken');
    // Optionally redirect to login page or home page via useNavigate() if called from a component
  };

  // if (loading) {
  //   return <div>Loading application...</div>; // Or a proper spinner component
  // }

  return (
    <AuthContext.Provider value={{ user, token, login, logout, isAuthenticated: !!user, loading }}>
      {children}
    </AuthContext.Provider>
  );
};
