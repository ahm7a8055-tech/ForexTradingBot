// jest.config.js
export default {
  testEnvironment: 'jest-environment-jsdom',
  setupFilesAfterEnv: ['<rootDir>/jest.setup.js'], // Or directly '@testing-library/jest-dom' if preferred
  transform: {
    '^.+\\.(js|jsx|ts|tsx)$': 'babel-jest',
  },
  moduleNameMapper: {
    '\\.(css|less|scss|sass)$': 'identity-obj-proxy', // Mock CSS imports
    '\\.(gif|ttf|eot|svg|png)$': '<rootDir>/__mocks__/fileMock.js', // Mock other static assets
  },
  moduleDirectories: ['node_modules', 'src'],
  // If using ES modules in your source files, ensure Babel is configured for them
  // or consider using experimental ESM support in Jest if appropriate for your Node version.
  // For Vite projects, ensure Jest can handle Vite's specific path aliases or environment variables if any.
};
