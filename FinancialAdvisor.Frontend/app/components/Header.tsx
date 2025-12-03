import React from 'react';
import { UserCircle } from 'lucide-react';

export function Header() {
  return (
    <header className="border-b bg-white dark:bg-zinc-950 px-6 py-4 flex items-center justify-between">
      <div className="flex items-center gap-2">
        <div className="size-8 bg-blue-600 rounded-lg flex items-center justify-center">
          <span className="text-white font-bold text-lg">FA</span>
        </div>
        <h1 className="text-xl font-bold text-zinc-900 dark:text-zinc-50">Financial Advisor</h1>
      </div>
      
      <button 
        className="p-2 hover:bg-zinc-100 dark:hover:bg-zinc-800 rounded-full transition-colors"
        aria-label="Profile"
        onClick={() => alert("Privacy and security settings (not implemented in this demo).")}
      >
        <UserCircle className="size-6 text-zinc-600 dark:text-zinc-400" />
      </button>
    </header>
  );
}
