import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';

const SystemSettingsPage = () => {
  const [isProcessing, setIsProcessing] = useState(false);
  const [feedbackMessage, setFeedbackMessage] = useState('');
  // const navigate = useNavigate(); // Uncomment if navigating to /login after 'restart'

  const handleApplyAndRestart = async () => {
    setIsProcessing(true);
    setFeedbackMessage('Applying new configurations...');
    console.log('[Admin Dashboard] Conceptual: Staging configuration update.');

    // Simulate API call or processing delay for applying settings
    await new Promise(resolve => setTimeout(resolve, 1500));

    setFeedbackMessage('Configurations applied. Initiating application restart...');
    console.log('[Admin Dashboard] Conceptual: Initiating application restart.');

    // Simulate API call or processing delay for initiating restart
    await new Promise(resolve => setTimeout(resolve, 1000));

    // Simulate restart
    // Option 1: Full page reload
    window.location.reload();

    // Option 2: Navigate to login (might be better if session is expected to be invalidated)
    // navigate('/login', { replace: true });
    // setIsProcessing(false); // Only needed if not reloading
    // setFeedbackMessage('Restart signal sent. Please re-login if necessary.'); // Only if not reloading
  };

  return (
    <div>
      <h2 className="text-2xl font-semibold mb-6 text-gray-800 dark:text-white">System Settings & Control</h2>

      <div className="bg-white dark:bg-gray-800 shadow-md rounded-lg p-6">
        <h3 className="text-xl font-medium mb-4 text-gray-700 dark:text-gray-200">Application Control</h3>
        <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
          Use the button below to apply any pending configurations and restart the application.
          This action will cause a brief interruption of service.
        </p>

        <button
          onClick={handleApplyAndRestart}
          disabled={isProcessing}
          className="px-6 py-3 bg-red-600 hover:bg-red-700 dark:bg-red-700 dark:hover:bg-red-800 text-white font-semibold rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2 dark:focus:ring-offset-gray-800 disabled:opacity-70 disabled:cursor-not-allowed transition ease-in-out duration-150"
        >
          {isProcessing ? (
            <div className="flex items-center">
              <svg className="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              Processing...
            </div>
          ) : (
            'Apply Configurations & Restart Application'
          )}
        </button>

        {feedbackMessage && (
          <p className="mt-4 text-sm text-indigo-600 dark:text-indigo-400 animate-pulse">
            {feedbackMessage}
          </p>
        )}
      </div>

      {/* Placeholder for other system settings if needed later */}
      {/* For example, view current appsettings.prod.json content (read-only for now) */}

    </div>
  );
};

export default SystemSettingsPage;
