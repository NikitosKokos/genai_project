import React from 'react';

export function WhatsNewPanel() {
  return (
    <div className="bg-white dark:bg-zinc-900 rounded-xl border p-6 h-full">
      <h2 className="text-lg font-bold mb-4">What's New for You</h2>
      <div className="space-y-4">
        {/* Placeholder Content */}
        <div className="p-4 bg-zinc-50 dark:bg-zinc-800 rounded-lg">
          <p className="text-sm text-zinc-600 dark:text-zinc-400 mb-1">Market Update</p>
          <p className="font-medium">Tech sector shows strong momentum today.</p>
        </div>
        <div className="p-4 bg-zinc-50 dark:bg-zinc-800 rounded-lg">
          <p className="text-sm text-zinc-600 dark:text-zinc-400 mb-1">Portfolio Alert</p>
          <p className="font-medium">Your simulated position in NVDA is up 2.5%.</p>
        </div>
      </div>
    </div>
  );
}
