import React from 'react';

export function AssetsPanel() {
  return (
    <div className="bg-white dark:bg-zinc-900 rounded-xl border p-6 h-full">
      <h2 className="text-lg font-bold mb-4">Available Assets</h2>
      <div className="space-y-3">
        {/* Placeholder Items */}
        <div className="flex items-center justify-between p-3 border rounded-lg hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors cursor-pointer">
          <div className="flex items-center gap-3">
            <div className="size-8 bg-zinc-100 dark:bg-zinc-700 rounded-full flex items-center justify-center text-xs font-bold">
              MSFT
            </div>
            <div>
              <p className="font-medium">Microsoft Corp</p>
              <p className="text-xs text-zinc-500">Tech</p>
            </div>
          </div>
          <div className="text-right">
            <p className="font-medium">$415.20</p>
            <button className="text-xs text-blue-600 hover:underline mt-1">Trade</button>
          </div>
        </div>
        
        <div className="flex items-center justify-between p-3 border rounded-lg hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors cursor-pointer">
          <div className="flex items-center gap-3">
            <div className="size-8 bg-zinc-100 dark:bg-zinc-700 rounded-full flex items-center justify-center text-xs font-bold">
              GOOGL
            </div>
            <div>
              <p className="font-medium">Alphabet Inc</p>
              <p className="text-xs text-zinc-500">Tech</p>
            </div>
          </div>
          <div className="text-right">
            <p className="font-medium">$175.30</p>
            <button className="text-xs text-blue-600 hover:underline mt-1">Trade</button>
          </div>
        </div>
      </div>
    </div>
  );
}
