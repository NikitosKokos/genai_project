'use client';

import React, { useState, useRef, useEffect } from 'react';
import { Send, X, Minimize2 } from 'lucide-react';
import { useUI } from '../context/UIContext';
import { api } from '@/lib/api';

interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

export function ChatWidget() {
  const { isModalOpen } = useUI();
  const [isExpanded, setIsExpanded] = useState(false);
  const [messages, setMessages] = useState<Message[]>([]);
  const [inputValue, setInputValue] = useState('');
  const [isTyping, setIsTyping] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    if (isExpanded) {
      scrollToBottom();
      inputRef.current?.focus();
    }
  }, [messages, isExpanded]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputValue.trim() || isTyping) return;

    const userMessage: Message = {
      id: Date.now().toString(),
      role: 'user',
      content: inputValue,
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMessage]);
    setInputValue('');
    setIsTyping(true);
    setIsExpanded(true);

    try {
      const response: any = await api.chat.sendMessage(inputValue);
      
      const assistantMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: response.message || "I'm having trouble connecting to the server.",
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, assistantMessage]);
    } catch (error) {
      console.error('Chat error:', error);
      const errorMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: "Sorry, I couldn't reach the financial advisor service. Please check if the backend is running.",
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, errorMessage]);
    } finally {
      setIsTyping(false);
    }
  };

  const handleInputFocus = () => {
    // Do not expand on focus, only on send
  };

  if (isModalOpen) return null;

  return (
    <>
      {/* Collapsed State - Small Input Bar */}
      {!isExpanded && (
        <div className="fixed bottom-6 left-1/2 -translate-x-1/2 w-full max-w-3xl z-50">
          <form onSubmit={handleSubmit} className="flex gap-2 items-center">
            <input
              ref={inputRef}
              type="text"
              placeholder="Ask me anything..."
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              onFocus={handleInputFocus}
              className="flex-1 px-6 py-4 bg-white dark:bg-zinc-900 border-2 border-zinc-200 dark:border-zinc-800 rounded-full shadow-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-zinc-900 text-zinc-900 dark:text-zinc-100 placeholder-zinc-400"
            />
            <button
              type="submit"
              className="bg-blue-600 text-white p-4 rounded-full shadow-lg hover:bg-blue-700 hover:scale-110 active:scale-95 transition-all duration-200 flex items-center justify-center disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100"
              disabled={!inputValue.trim()}
            >
              <Send className="size-5" />
            </button>
          </form>
        </div>
      )}

      {/* Expanded State - Full Chat Interface */}
      {isExpanded && (
        <div className="fixed bottom-0 left-1/2 -translate-x-1/2 w-full max-w-3xl bg-white dark:bg-zinc-900 border-x border-t rounded-t-2xl shadow-2xl flex flex-col h-[600px] z-50 animate-in slide-in-from-bottom duration-500 ease-out">
          {/* Header */}
          <div className="p-4 border-b bg-gradient-to-r from-blue-50 to-indigo-50 dark:from-zinc-800 dark:to-zinc-900 rounded-t-2xl flex justify-between items-center">
            <div className="flex items-center gap-3">
              <div className="size-10 bg-blue-600 rounded-full flex items-center justify-center">
                <span className="text-white font-bold text-sm">AI</span>
              </div>
              <div>
                <h3 className="font-semibold text-zinc-900 dark:text-zinc-100">AI Financial Assistant</h3>
                <span className="text-xs text-zinc-500 flex items-center gap-1">
                  <span className="size-2 bg-green-500 rounded-full"></span>
                  Connected
                </span>
              </div>
            </div>
            <button
              onClick={() => setIsExpanded(false)}
              className="p-2 hover:bg-zinc-100 dark:hover:bg-zinc-800 hover:scale-110 active:scale-95 rounded-full transition-all duration-200"
              aria-label="Minimize chat"
            >
              <Minimize2 className="size-5 text-zinc-600 dark:text-zinc-400" />
            </button>
          </div>

          {/* Messages Area */}
          <div className="flex-1 p-4 overflow-y-auto space-y-4 bg-zinc-50/50 dark:bg-zinc-950/50">
            {messages.map((message) => (
              <div
                key={message.id}
                className={`flex ${message.role === 'user' ? 'justify-end' : 'justify-start'}`}
              >
                <div
                  className={`max-w-[80%] p-3 rounded-2xl ${
                    message.role === 'user'
                      ? 'bg-blue-600 text-white rounded-tr-none'
                      : 'bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 rounded-tl-none shadow-sm'
                  }`}
                >
                  <p className="text-sm leading-relaxed">{message.content}</p>
                  <p className="text-xs mt-1 opacity-70">
                    {message.timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                  </p>
                </div>
              </div>
            ))}
            {isTyping && (
              <div className="flex justify-start animate-in fade-in slide-in-from-bottom-2 duration-300">
                <div className="bg-white dark:bg-zinc-800 p-4 rounded-2xl rounded-tl-none shadow-sm flex items-center gap-1.5">
                  <div className="size-2 bg-zinc-400 rounded-full animate-bounce [animation-delay:-0.3s]"></div>
                  <div className="size-2 bg-zinc-400 rounded-full animate-bounce [animation-delay:-0.15s]"></div>
                  <div className="size-2 bg-zinc-400 rounded-full animate-bounce"></div>
                </div>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>

          {/* Input Area */}
          <div className="p-4 border-t bg-white dark:bg-zinc-900 rounded-b-2xl">
            <form onSubmit={handleSubmit} className="flex gap-2">
              <input
                ref={inputRef}
                type="text"
                placeholder="Ask me anything..."
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                className="flex-1 px-4 py-3 border-2 border-zinc-200 dark:border-zinc-700 rounded-full focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-zinc-950 dark:text-zinc-100 transition-all"
                disabled={isTyping}
              />
              <button
                type="submit"
                className="bg-blue-600 text-white px-6 py-3 rounded-full font-medium hover:bg-blue-700 hover:scale-105 active:scale-95 transition-all duration-200 flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100"
                disabled={!inputValue.trim() || isTyping}
              >
                <Send className="size-4" />
                Send
              </button>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
