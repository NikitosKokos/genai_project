'use client';

import React, { useState } from 'react';
import { UserCircle, CandlestickChart, Sparkles, X, Check } from 'lucide-react';
import { useUI } from '../context/UIContext';

export function Header() {
  const { setModalOpen } = useUI();
  const [isAboutOpen, setIsAboutOpen] = useState(false);

  const handleLogoClick = () => {
    setIsAboutOpen(true);
    setModalOpen(true);
  };

  return (
    <>
    <header className="border-b bg-white dark:bg-zinc-950 px-6 py-4 flex items-center justify-between sticky top-0 z-40 backdrop-blur-sm bg-white/80 dark:bg-zinc-950/80">
      <div 
        className="flex items-center gap-3 cursor-pointer group"
        onClick={handleLogoClick}
      >
        <div className="size-10 flex items-center justify-center relative transition-transform duration-300 group-hover:scale-105">
          <svg width="40" height="40" viewBox="0 0 40 40" fill="none" xmlns="http://www.w3.org/2000/svg">
            {/* Green Candle */}
            <rect x="8" y="12" width="10" height="16" rx="2" fill="#10b981" />
            <line x1="13" y1="8" x2="13" y2="32" stroke="#10b981" strokeWidth="2" strokeLinecap="round" />
            
            {/* Red Candle */}
            <rect x="22" y="16" width="10" height="12" rx="2" fill="#ef4444" />
            <line x1="27" y1="10" x2="27" y2="30" stroke="#ef4444" strokeWidth="2" strokeLinecap="round" />
          </svg>
          <div className="absolute -top-1 -right-1 size-3 bg-yellow-400 rounded-full border-2 border-white dark:border-zinc-950 flex items-center justify-center">
            <Sparkles className="size-2 text-yellow-900" />
          </div>
        </div>
        <div>
          <h1 className="text-xl font-bold text-zinc-900 dark:text-zinc-50 flex items-center gap-1 group-hover:text-emerald-500 transition-colors">
            Nova
          </h1>
          <p className="text-xs text-zinc-500 font-medium group-hover:text-emerald-600/70 transition-colors">AI WealthOS</p>
        </div>
      </div>
      
      <button 
        className="p-2 hover:bg-zinc-100 dark:hover:bg-zinc-800 hover:scale-110 active:scale-95 rounded-full transition-all duration-200"
        aria-label="Profile"
        onClick={() => alert("Privacy and security settings (not implemented in this demo).")}
      >
        <UserCircle className="size-6 text-zinc-600 dark:text-zinc-400" />
      </button>
    </header>

    {isAboutOpen && (
      <div 
        className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-in fade-in duration-200"
        onClick={() => {
          setIsAboutOpen(false);
          setModalOpen(false);
        }}
      >
        <div 
          className="bg-white dark:bg-zinc-900 rounded-2xl shadow-2xl w-full max-w-lg p-8 animate-in zoom-in-95 duration-200 relative overflow-hidden"
          onClick={(e) => e.stopPropagation()}
        >
          <div className="absolute top-0 right-0 p-4">
            <button 
              onClick={() => {
                setIsAboutOpen(false);
                setModalOpen(false);
              }}
              className="p-2 hover:bg-zinc-100 dark:hover:bg-zinc-800 rounded-full transition-colors"
            >
              <X className="size-5" />
            </button>
          </div>
          
          <div className="flex flex-col items-center text-center mb-8">
            <div className="size-16 bg-gradient-to-br from-emerald-500 to-teal-600 rounded-2xl flex items-center justify-center shadow-xl shadow-emerald-500/20 mb-4">
              <CandlestickChart className="size-8 text-white" />
            </div>
            <h2 className="text-3xl font-bold mb-2">Welcome to Nova</h2>
            <p className="text-zinc-500 text-lg">Your Intelligent Wealth Operating System</p>
          </div>

          <div className="space-y-6">
            <p className="text-zinc-700 dark:text-zinc-300 leading-relaxed">
              Nova simplifies your financial life by combining advanced AI insights with intuitive portfolio management. 
              Whether you're tracking performance or discovering new assets, Nova brings everything into one seamless platform.
            </p>

            <div className="grid grid-cols-2 gap-4">
              <div className="p-4 bg-zinc-50 dark:bg-zinc-800/50 rounded-xl border border-zinc-100 dark:border-zinc-800">
                <h3 className="font-semibold mb-1 flex items-center gap-2">
                  <Sparkles className="size-4 text-emerald-500" /> AI Insights
                </h3>
                <p className="text-xs text-zinc-500">Real-time market intelligence tailored to your portfolio.</p>
              </div>
              <div className="p-4 bg-zinc-50 dark:bg-zinc-800/50 rounded-xl border border-zinc-100 dark:border-zinc-800">
                <h3 className="font-semibold mb-1 flex items-center gap-2">
                  <Check className="size-4 text-emerald-500" /> One Platform
                </h3>
                <p className="text-xs text-zinc-500">Trade, track, and analyze all your assets in one place.</p>
              </div>
            </div>

            <button 
              onClick={() => {
                setIsAboutOpen(false);
                setModalOpen(false);
              }}
              className="w-full py-4 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl font-bold text-lg shadow-lg shadow-emerald-500/25 transition-all"
            >
              Get Started
            </button>
          </div>
        </div>
      </div>
    )}
    </>
  );
}
