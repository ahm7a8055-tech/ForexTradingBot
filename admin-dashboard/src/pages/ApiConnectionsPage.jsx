import React, { useState, useEffect } from 'react';
import { getClientSettings, updateClientSettings } from '../services/telegramConfigService';

const ApiConnectionsPage = () => {
  const [apiId, setApiId] = useState('');
  const [apiHash, setApiHash] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(null);
  const [successMessage, setSuccessMessage] = useState('');

  useEffect(() => {
    const fetchSettings = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const settings = await getClientSettings();
        setApiId(settings.apiId ? String(settings.apiId) : '');
        setApiHash(settings.apiHash || '');
      } catch (err) {
        setError('Failed to fetch client settings: ' + err.message);
        console.error(err);
      } finally {
        setIsLoading(false);
      }
    };
    fetchSettings();
  }, []);

  const handleSubmitClientSettings = async (e) => {
    e.preventDefault();
    setIsLoading(true);
    setError(null);
    setSuccessMessage('');
    try {
      const settingsToUpdate = {
        apiId: apiId ? parseInt(apiId, 10) : 0, // Ensure apiId is an integer
        apiHash,
      };
      await updateClientSettings(settingsToUpdate);
      setSuccessMessage('Client settings updated successfully!');
    } catch (err) {
      setError('Failed to update client settings: ' + err.message);
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="container mx-auto p-4">
      <h1 className="text-3xl font-bold mb-6 text-gray-800 dark:text-white">API Connections Management</h1>

      {/* Placeholder for other API connection strings */}
      <div className="mb-8 p-6 bg-white dark:bg-gray-800 shadow-md rounded-lg">
        <h2 className="text-2xl font-semibold mb-3 text-gray-700 dark:text-gray-200">External API Configurations</h2>
        <p className="text-sm text-gray-600 dark:text-gray-400 mb-2">
          This section is a placeholder for managing connection strings or API keys for other external services
          that the ForexTradingBot might integrate with (e.g., financial data providers, notification services).
        </p>
        <p className="text-sm text-gray-600 dark:text-gray-400">
          Currently, primary external connections like Telegram are managed in their specific sections below.
        </p>
        {/* TODO: Implement Feature 1 UI as needed */}
      </div>

      {/* Telegram Client Settings Section */}
      <div className="p-6 bg-white dark:bg-gray-800 shadow-md rounded-lg">
        <h2 className="text-2xl font-semibold mb-6 text-gray-700 dark:text-gray-200">Telegram Client Settings</h2>
        <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
          Configure the API ID and API Hash for the Telegram client library (e.g., Telethon, WTelegramClient).
          These are required if the application needs to act as a Telegram user account (user bot) for features like channel scraping or advanced interactions, distinct from the main Telegram Bot account.
        </p>

        {error && <div className="mb-4 p-3 bg-red-100 text-red-700 border border-red-400 rounded">{error}</div>}
        {successMessage && <div className="mb-4 p-3 bg-green-100 text-green-700 border border-green-400 rounded">{successMessage}</div>}

        <form onSubmit={handleSubmitClientSettings}>
          <div className="mb-4">
            <label htmlFor="apiId" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">API ID</label>
            <p className="text-xs text-gray-500 dark:text-gray-400 mb-1">
              Your personal Telegram API ID, obtained from <a href="https://my.telegram.org/apps" target="_blank" rel="noopener noreferrer" className="text-indigo-600 hover:underline dark:text-indigo-400">my.telegram.org/apps</a>.
            </p>
            <input
              type="text" // Using text for easier input, will parse to int
              id="apiId"
              value={apiId}
              onChange={(e) => setApiId(e.target.value)}
              className="mt-1 block w-full px-3 py-2 bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm text-gray-900 dark:text-gray-100"
              placeholder="Enter Telegram API ID"
              disabled={isLoading}
            />
          </div>

          <div className="mb-6">
            <label htmlFor="apiHash" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">API Hash</label>
            <p className="text-xs text-gray-500 dark:text-gray-400 mb-1">
              Your personal Telegram API Hash, obtained alongside the API ID from <a href="https://my.telegram.org/apps" target="_blank" rel="noopener noreferrer" className="text-indigo-600 hover:underline dark:text-indigo-400">my.telegram.org/apps</a>.
            </p>
            <input
              type="text"
              id="apiHash"
              value={apiHash}
              onChange={(e) => setApiHash(e.target.value)}
              className="mt-1 block w-full px-3 py-2 bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm text-gray-900 dark:text-gray-100"
              placeholder="Enter Telegram API Hash"
              disabled={isLoading}
            />
          </div>

          <button
            type="submit"
            className="w-full px-4 py-2 bg-indigo-600 text-white font-semibold rounded-md shadow-sm hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50"
            disabled={isLoading}
          >
            {isLoading ? 'Saving...' : 'Save Client Settings'}
          </button>
        </form>
      </div>
    </div>
  );
};

export default ApiConnectionsPage;
