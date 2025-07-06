import React, { useState, useEffect } from 'react';
import { getBotSettings, updateBotSettings } from '../services/telegramConfigService';

const AppSettingsPage = () => {
  const [botToken, setBotToken] = useState('');
  const [adminUserIds, setAdminUserIds] = useState(''); // Comma-separated string
  const [chatIdForLogs, setChatIdForLogs] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(null);
  const [successMessage, setSuccessMessage] = useState('');

  useEffect(() => {
    const fetchSettings = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const settings = await getBotSettings();
        setBotToken(settings.botToken || '');
        // Convert array of admin IDs to comma-separated string for the input field
        setAdminUserIds(settings.adminUserIds ? settings.adminUserIds.join(', ') : '');
        setChatIdForLogs(settings.chatIdForLogs ? String(settings.chatIdForLogs) : '');
      } catch (err) {
        setError('Failed to fetch bot settings: ' + err.message);
        console.error(err);
      } finally {
        setIsLoading(false);
      }
    };
    fetchSettings();
  }, []);

  const handleSubmitBotSettings = async (e) => {
    e.preventDefault();
    setIsLoading(true);
    setError(null);
    setSuccessMessage('');
    try {
      // Convert comma-separated string of admin IDs to array of numbers
      const adminIdsArray = adminUserIds.split(',')
        .map(id => parseInt(id.trim(), 10))
        .filter(id => !isNaN(id)); // Filter out any NaN values

      const settingsToUpdate = {
        botToken,
        adminUserIds: adminIdsArray,
        chatIdForLogs: chatIdForLogs ? parseInt(chatIdForLogs, 10) : null,
      };
      await updateBotSettings(settingsToUpdate);
      setSuccessMessage('Bot settings updated successfully!');
    } catch (err) {
      setError('Failed to update bot settings: ' + err.message);
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="container mx-auto p-4">
      <h1 className="text-3xl font-bold mb-6 text-gray-800 dark:text-white">Application Settings</h1>

      {/* Placeholder for other app settings */}
      <div className="mb-8 p-6 bg-white dark:bg-gray-800 shadow-md rounded-lg">
        <h2 className="text-2xl font-semibold mb-3 text-gray-700 dark:text-gray-200">General Application Configuration</h2>
        <p className="text-sm text-gray-600 dark:text-gray-400 mb-2">
          This section is a placeholder for any other general application settings that might be exposed for configuration.
        </p>
        <p className="text-sm text-gray-600 dark:text-gray-400">
          Currently, most critical configurations like database connection strings are managed in the backend's `appsettings.json` or environment variables.
        </p>
        {/* TODO: Implement Feature 2 UI as needed */}
      </div>

      {/* Telegram Bot Settings Section */}
      <div className="p-6 bg-white dark:bg-gray-800 shadow-md rounded-lg">
        <h2 className="text-2xl font-semibold mb-6 text-gray-700 dark:text-gray-200">Telegram Bot Settings</h2>
        <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
          Configure the primary settings for your Telegram bot's operation. These settings are crucial for the bot to connect to Telegram and for administrators to interact with it.
        </p>

        {error && <div className="mb-4 p-3 bg-red-100 text-red-700 border border-red-400 rounded">{error}</div>}
        {successMessage && <div className="mb-4 p-3 bg-green-100 text-green-700 border border-green-400 rounded">{successMessage}</div>}

        <form onSubmit={handleSubmitBotSettings}>
          <div className="mb-4">
            <label htmlFor="botToken" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Bot Token</label>
            <p className="text-xs text-gray-500 dark:text-gray-400 mb-1">
              The unique authentication token provided by BotFather on Telegram. This is required for the bot to connect.
            </p>
            <input
              type="text"
              id="botToken"
              value={botToken}
              onChange={(e) => setBotToken(e.target.value)}
              className="mt-1 block w-full px-3 py-2 bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm text-gray-900 dark:text-gray-100"
              placeholder="Enter Telegram Bot Token"
              disabled={isLoading}
            />
          </div>

          <div className="mb-4">
            <label htmlFor="adminUserIds" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Admin User IDs (comma-separated)</label>
            <p className="text-xs text-gray-500 dark:text-gray-400 mb-1">
              A list of numeric Telegram User IDs that should have administrative privileges for this bot. Separate multiple IDs with a comma.
            </p>
            <input
              type="text"
              id="adminUserIds"
              value={adminUserIds}
              onChange={(e) => setAdminUserIds(e.target.value)}
              className="mt-1 block w-full px-3 py-2 bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm text-gray-900 dark:text-gray-100"
              placeholder="e.g., 123456, 789012"
              disabled={isLoading}
            />
          </div>

          <div className="mb-6">
            <label htmlFor="chatIdForLogs" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Chat ID for Logs (Optional)</label>
            <p className="text-xs text-gray-500 dark:text-gray-400 mb-1">
              A specific Telegram Chat ID (user, group, or channel) where the bot can send operational logs or alerts. Can be left empty if not needed.
            </p>
            <input
              type="text"
              id="chatIdForLogs"
              value={chatIdForLogs}
              onChange={(e) => setChatIdForLogs(e.target.value)}
              className="mt-1 block w-full px-3 py-2 bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm text-gray-900 dark:text-gray-100"
              placeholder="Enter Chat ID for sending logs"
              disabled={isLoading}
            />
          </div>

          <button
            type="submit"
            className="w-full px-4 py-2 bg-indigo-600 text-white font-semibold rounded-md shadow-sm hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50"
            disabled={isLoading}
          >
            {isLoading ? 'Saving...' : 'Save Bot Settings'}
          </button>
        </form>
      </div>
    </div>
  );
};

export default AppSettingsPage;
