// admin-dashboard/src/services/mockApiConfigService.js

// Simulate a delay for API calls
const API_DELAY = 500;

let mockConnectionStrings = [
  {
    id: 'db_primary',
    name: 'Primary Database',
    connectionString: 'Server=tcp:primaryserver.database.windows.net,1433;Initial Catalog=primary_db;User ID=user;Password=********;',
    status: 'connected', // 'connected', 'disconnected', 'testing', 'error'
    isActive: true
  },
  {
    id: 'db_replica',
    name: 'Read Replica Database',
    connectionString: 'Server=tcp:replicaserver.database.windows.net,1433;Initial Catalog=replica_db;User ID=user;Password=********;',
    status: 'disconnected',
    isActive: false
  },
  {
    id: 'service_bus',
    name: 'Azure Service Bus',
    connectionString: 'Endpoint=sb://myservicebus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=********',
    status: 'connected',
    isActive: false
  },
  {
    id: 'redis_cache',
    name: 'Redis Cache',
    connectionString: 'myredishost.redis.cache.windows.net:6380,password=********,ssl=True,abortConnect=False',
    status: 'error',
    isActive: false
  }
];

export const getConnectionStrings = () => {
  return new Promise((resolve) => {
    setTimeout(() => {
      resolve([...mockConnectionStrings]); // Return a copy
    }, API_DELAY);
  });
};

export const updateConnectionString = (id, newStringValue) => {
  return new Promise((resolve, reject) => {
    setTimeout(() => {
      const stringIndex = mockConnectionStrings.findIndex(cs => cs.id === id);
      if (stringIndex !== -1) {
        mockConnectionStrings[stringIndex].connectionString = newStringValue;
        // Simulate that updating might change status until tested
        mockConnectionStrings[stringIndex].status = 'disconnected';
        resolve({ ...mockConnectionStrings[stringIndex] });
      } else {
        reject(new Error('Connection string not found.'));
      }
    }, API_DELAY);
  });
};

export const testConnectionString = (id) => {
  return new Promise((resolve, reject) => {
    const stringIndex = mockConnectionStrings.findIndex(cs => cs.id === id);
    if (stringIndex === -1) {
      setTimeout(() => reject(new Error('Connection string not found for testing.')), API_DELAY);
      return;
    }

    // Simulate testing process
    mockConnectionStrings[stringIndex].status = 'testing';
    // Force a re-render if a component is listening directly, though typically state management handles this.
    // This mock service doesn't directly trigger re-renders.

    setTimeout(() => {
      // Simulate random success/failure, or base it on some characteristic of the string/id
      const success = Math.random() > 0.3 || id === 'db_primary'; // Make primary db more likely to succeed
      if (success) {
        mockConnectionStrings[stringIndex].status = 'connected';
        resolve({ id, status: 'connected', message: 'Connection successful!' });
      } else {
        mockConnectionStrings[stringIndex].status = 'error';
        resolve({ id, status: 'error', message: 'Connection failed. Please check details.' });
      }
    }, API_DELAY * 3); // Longer delay for testing
  });
};

// Optional: function to set an active connection string if that's a feature
export const setActiveConnectionString = (id) => {
  return new Promise((resolve) => {
    setTimeout(() => {
      mockConnectionStrings = mockConnectionStrings.map(cs => ({
        ...cs,
        isActive: cs.id === id,
      }));
      resolve(mockConnectionStrings.find(cs => cs.id === id));
    }, API_DELAY);
  });
};

// Note: This mock service modifies in-memory data.
// For more robust testing between test files, data might need to be reset.
// However, for component tests interacting with this, it should be fine per test suite.
