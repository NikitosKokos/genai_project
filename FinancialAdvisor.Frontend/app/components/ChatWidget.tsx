import React from 'react';

export function ChatWidget() {
  return (
    <div className="fixed bottom-0 left-1/2 -translate-x-1/2 w-full max-w-3xl bg-white dark:bg-zinc-900 border-x border-t rounded-t-xl shadow-2xl flex flex-col h-[500px]">
      <div className="p-4 border-b bg-zinc-50 dark:bg-zinc-800 rounded-t-xl flex justify-between items-center">
        <h3 className="font-semibold text-zinc-700 dark:text-zinc-200">AI Financial Assistant</h3>
        <span className="text-xs text-zinc-500">Connected</span>
      </div>
      
      <div className="flex-1 p-4 overflow-y-auto space-y-4">
        <div className="flex justify-start">
          <div className="bg-zinc-100 dark:bg-zinc-800 p-3 rounded-2xl rounded-tl-none max-w-[80%]">
            <p className="text-sm">Hello! I'm your financial advisor assistant. How can I help you today? I can analyze your portfolio, simulate trades, or answer market questions.</p>
          </div>
        </div>
      </div>

      <div className="p-4 border-t">
        <form className="flex gap-2" onSubmit={(e) => e.preventDefault()}>
          <input 
            type="text" 
            placeholder="Ask me anything..." 
            className="flex-1 px-4 py-2 border rounded-full focus:outline-none focus:ring-2 focus:ring-blue-500 dark:bg-zinc-950 dark:border-zinc-700"
          />
          <button 
            type="submit"
            className="bg-blue-600 text-white px-6 py-2 rounded-full font-medium hover:bg-blue-700 transition-colors"
          >
            Send
          </button>
        </form>
      </div>
    </div>
  );
}
