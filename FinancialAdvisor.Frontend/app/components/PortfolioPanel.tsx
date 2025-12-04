import React from 'react';

export function PortfolioPanel() {
  return (
    <div className="bg-white dark:bg-zinc-900 rounded-xl border p-6 h-full">
      <h2 className="text-lg font-bold mb-4">Portfolio Performance</h2>
      <div className="h-64 bg-zinc-50 dark:bg-zinc-800 rounded-lg flex items-center justify-center border border-dashed border-zinc-300 dark:border-zinc-700">
        <p className="text-zinc-500">Portfolio Chart Placeholder</p>
      </div>
      <div className="mt-6">
        <h3 className="font-semibold mb-3">Current Positions</h3>
        <div className="space-y-2">
          <div className="flex justify-between p-3 bg-zinc-50 dark:bg-zinc-800 rounded-lg">
            <div>
              <p className="font-bold">AAPL</p>
              <p className="text-xs text-zinc-500">10 shares</p>
            </div>
            <div className="text-right">
              <p className="font-bold">$1,850.00</p>
              <p className="text-xs text-green-600">+1.2%</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
