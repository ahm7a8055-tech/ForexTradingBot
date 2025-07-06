import React from 'react';

const FullPageSpinner = () => {
  return (
    <div className="flex items-center justify-center min-h-screen bg-gray-100 dark:bg-gray-900">
      <div className="w-16 h-16 border-4 border-blue-500 border-dashed rounded-full animate-spin border-t-transparent"></div>
      {/* Basic spinner, can be enhanced with Tailwind UI components or SVGs later if needed */}
    </div>
  );
};

export default FullPageSpinner;
