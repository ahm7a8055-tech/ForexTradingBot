import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getConnectivityStatus } from '../services/telegramConfigService'; // Import the new service

const SystemSettingsPage = () => {
  const [isProcessingRestart, setIsProcessingRestart] = useState(false);
  const [feedbackMessageRestart, setFeedbackMessageRestart] = useState('');
  // const navigate = useNavigate(); // Uncomment if navigating to /login after 'restart'

  const [connectivityStatus, setConnectivityStatus] = useState(null);
  const [isCheckingConnectivity, setIsCheckingConnectivity] = useState(false);
  const [connectivityError, setConnectivityError] = useState('');

  const handleApplyAndRestart = async () => {
    // ... (existing code for restart) ...
    setIsProcessingRestart(true);
    setFeedbackMessageRestart('Applying new configurations...');
    console.log('[Admin Dashboard] Conceptual: Staging configuration update.');
    await new Promise(resolve => setTimeout(resolve, 1500));
    setFeedbackMessageRestart('Configurations applied. Initiating application restart...');
    console.log('[Admin Dashboard] Conceptual: Initiating application restart.');
    await new Promise(resolve => setTimeout(resolve, 1000));
    window.location.reload();
  };

  const handleCheckConnectivity = async () => {
    setIsCheckingConnectivity(true);
    setConnectivityError('');
    setConnectivityStatus(null);
    try {
      const status = await getConnectivityStatus();
      setConnectivityStatus(status);
    } catch (err) {
      setConnectivityError('Failed to fetch connectivity status: ' + err.message);
      console.error(err);
    } finally {
      setIsCheckingConnectivity(false);
    }
  };

  const StatusIndicator = ({ isSuccess, successText, errorText, details }) => (
    <div className={`flex items-center p-3 rounded-md text-sm ${isSuccess ? 'bg-green-100 dark:bg-green-900 text-green-700 dark:text-green-200' : 'bg-red-100 dark:bg-red-900 text-red-700 dark:text-red-200'}`}>
      <span className={`mr-2 h-4 w-4 rounded-full ${isSuccess ? 'bg-green-500' : 'bg-red-500'}`}></span>
      <div>
        <p className="font-semibold">{isSuccess ? successText : errorText}</p>
        {details && <p className="text-xs opacity-80">{details}</p>}
      </div>
    </div>
  );


  return (
    <div className="container mx-auto p-4">
      <h1 className="text-3xl font-bold mb-8 text-gray-800 dark:text-white">System Settings & Diagnostics</h1>

      {/* Connectivity Status Section */}
      <div className="mb-8 p-6 bg-white dark:bg-gray-800 shadow-md rounded-lg">
        <h2 className="text-xl font-semibold mb-4 text-gray-700 dark:text-gray-200">System Connectivity Check</h2>
        <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
          Test connectivity to essential services like the database and Telegram API.
        </p>
        <button
          onClick={handleCheckConnectivity}
          disabled={isCheckingConnectivity}
          className="px-6 py-2 mb-4 bg-blue-600 hover:bg-blue-700 dark:bg-blue-500 dark:hover:bg-blue-600 text-white font-semibold rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 dark:focus:ring-offset-gray-800 disabled:opacity-70 disabled:cursor-not-allowed transition ease-in-out duration-150"
        >
          {isCheckingConnectivity ? (
            <div className="flex items-center">
              <svg className="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              Checking...
            </div>
          ) : (
            'Run Connectivity Checks'
          )}
        </button>

        {connectivityError && (
          <div className="mb-4 p-3 bg-red-100 text-red-700 border border-red-400 rounded text-sm">{connectivityError}</div>
        )}

        {connectivityStatus && !connectivityError && (
          <div className="space-y-3">
            <StatusIndicator
              isSuccess={connectivityStatus.canConnectToDatabase}
              successText={`Database Connected (${connectivityStatus.databaseProvider || 'N/A'})`}
              errorText={`Database Connection Failed (${connectivityStatus.databaseProvider || 'N/A'})`}
              details={connectivityStatus.databaseError || 'No errors.'}
            />
            <StatusIndicator
              isSuccess={connectivityStatus.canAccessTelegramApi}
              successText={`Telegram API Connected (Bot: @${connectivityStatus.telegramBotUsername || 'N/A'})`}
              errorText="Telegram API Connection Failed"
              details={connectivityStatus.telegramApiError || 'No errors.'}
            />
            {connectivityStatus.messages && connectivityStatus.messages.length > 0 && (
               <div className="mt-3 p-3 bg-gray-100 dark:bg-gray-700 rounded-md">
                 <h4 className="text-sm font-semibold text-gray-700 dark:text-gray-200 mb-1">Additional Messages:</h4>
                 <ul className="list-disc list-inside text-xs text-gray-600 dark:text-gray-400 space-y-1">
                   {connectivityStatus.messages.map((msg, index) => <li key={index}>{msg}</li>)}
                 </ul>
               </div>
            )}
          </div>
        )}
      </div>


      {/* Application Control Section (existing) */}
      <div className="bg-white dark:bg-gray-800 shadow-md rounded-lg p-6">
        <h3 className="text-xl font-semibold mb-4 text-gray-700 dark:text-gray-200">Application Control</h3>
        <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
          Use the button below to apply any pending configurations (if applicable from other settings areas) and trigger a restart of the backend application.
          This action will typically cause a brief interruption of service while the backend processes restart. Ensure all critical settings are saved before proceeding.
        </p>

        <button
          onClick={handleApplyAndRestart}
          disabled={isProcessingRestart}
          className="px-6 py-3 bg-red-600 hover:bg-red-700 dark:bg-red-700 dark:hover:bg-red-800 text-white font-semibold rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2 dark:focus:ring-offset-gray-800 disabled:opacity-70 disabled:cursor-not-allowed transition ease-in-out duration-150"
        >
          {isProcessingRestart ? (
            <div className="flex items-center">
              <svg className="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              Processing Restart...
            </div>
          ) : (
            'Apply Configurations & Restart Application'
          )}
        </button>

        {feedbackMessageRestart && (
          <p className="mt-4 text-sm text-indigo-600 dark:text-indigo-400 animate-pulse">
            {feedbackMessageRestart}
          </p>
        )}
      </div>
    </div>
  );
};

export default SystemSettingsPage;
