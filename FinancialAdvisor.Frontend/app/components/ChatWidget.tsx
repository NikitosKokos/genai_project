'use client';

import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Send, Minimize2, ChevronDown, Info, X, Sparkles, Zap } from 'lucide-react';
import { useUI } from '../context/UIContext';
import { api } from '@/lib/api';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeRaw from 'rehype-raw';
// Dynamic import for DOMPurify to avoid SSR issues
let DOMPurify: any;
if (typeof window !== 'undefined') {
  DOMPurify = require('isomorphic-dompurify');
}
import Image from 'next/image';

interface Message {
   id: string;
   role: 'user' | 'assistant';
   content: string;
   thinking?: string; // Chain-of-thought reasoning
   status?: string; // Current status (e.g., "Planning...", "Connecting...")
   timestamp: Date;
}

export function ChatWidget() {
   const { isModalOpen } = useUI();
   const [isExpanded, setIsExpanded] = useState(false);
   const [messages, setMessages] = useState<Message[]>([]);
   const [inputValue, setInputValue] = useState('');
   const [isTyping, setIsTyping] = useState(false);
   const [isModelDropdownOpen, setIsModelDropdownOpen] = useState(false);
   const [isSupernovaEnabled, setIsSupernovaEnabled] = useState(false);
   const [isInfoModalOpen, setIsInfoModalOpen] = useState(false);
   const messagesEndRef = useRef<HTMLDivElement>(null);
   const inputRef = useRef<HTMLInputElement>(null);
   const dropdownRef = useRef<HTMLDivElement>(null);
   const infoModalRef = useRef<HTMLDivElement>(null);

   // Refs for streaming - accumulate chunks without triggering re-renders
   const responseBufferRef = useRef('');
   const thinkingBufferRef = useRef('');
   const assistantMessageIdRef = useRef<string | null>(null);

   // Immediate update function - update state directly for real-time streaming
   const scheduleUpdate = useCallback(() => {
      if (assistantMessageIdRef.current) {
         const updateStartTime = performance.now();
         const contentLength = responseBufferRef.current.length;
         const thinkingLength = thinkingBufferRef.current.length;
         console.log(
            '[ChatWidget] scheduleUpdate: content=',
            contentLength,
            'thinking=',
            thinkingLength,
         );

         // Update immediately - React will batch if needed, but we want real-time updates
         setMessages((prev) => {
            const updated = prev.map((msg) => {
               if (msg.id === assistantMessageIdRef.current) {
                  const newMsg = {
                     ...msg,
                     content: responseBufferRef.current,
                     thinking: thinkingBufferRef.current || msg.thinking,
                  };
                  console.log(
                     '[ChatWidget] Updating message, new content length:',
                     newMsg.content.length,
                  );
                  return newMsg;
               }
               return msg;
            });
            const updateDuration = performance.now() - updateStartTime;
            console.log(
               '[ChatWidget] setMessages() completed, took',
               updateDuration.toFixed(2),
               'ms',
            );
            return updated;
         });
      } else {
         console.log('[ChatWidget] scheduleUpdate: No assistantMessageIdRef, skipping update');
      }
   }, []);

   const scrollToBottom = () => {
      messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
   };

   useEffect(() => {
      if (isExpanded) {
         scrollToBottom();
         inputRef.current?.focus();
      }
   }, [messages, isExpanded]);

   // Also scroll when content updates during streaming
   useEffect(() => {
      if (isExpanded && messages.length > 0) {
         const lastMessage = messages[messages.length - 1];
         if (lastMessage?.role === 'assistant' && lastMessage.content) {
            // Small delay to ensure DOM has updated
            const timer = setTimeout(() => {
               scrollToBottom();
            }, 50);
            return () => clearTimeout(timer);
         }
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
      setIsExpanded(true);
      setIsTyping(true);

      try {
         // Reset buffers for new message
         responseBufferRef.current = '';
         thinkingBufferRef.current = '';

         // Create assistant message immediately with initial status
         const assistantMessageId = (Date.now() + 1).toString();
         assistantMessageIdRef.current = assistantMessageId;
         setMessages((prev) => [
            ...prev,
            {
               id: assistantMessageId,
               role: 'assistant',
               content: '',
               thinking: '',
               status: 'Connecting...',
               timestamp: new Date(),
            },
         ]);

         let isFirstChunk = true;

         // Model configuration: Default vs Supernova
         const enableReasoning = isSupernovaEnabled;
         const documentCount = isSupernovaEnabled ? 6 : 3;

         await api.chat.streamMessage(
            userMessage.content,
            'test-session', // Using consistent session
            enableReasoning,
            documentCount,
            (chunk) => {
               console.log('[ChatWidget] ===== RESPONSE CHUNK RECEIVED =====');
               console.log('[ChatWidget] Time:', new Date().toISOString());
               console.log('[ChatWidget] Chunk length:', chunk.length);
               console.log('[ChatWidget] Chunk preview:', JSON.stringify(chunk.substring(0, 100)));
               console.log('[ChatWidget] Is first chunk:', isFirstChunk);

               // When we receive the first response chunk, clear status
               if (isFirstChunk) {
                  isFirstChunk = false;
                  // Clear status when content starts arriving
                  if (assistantMessageIdRef.current) {
                     setMessages((prev) => {
                        return prev.map((msg) => {
                           if (msg.id === assistantMessageIdRef.current) {
                              return {
                                 ...msg,
                                 status: undefined, // Clear status when content arrives
                              };
                           }
                           return msg;
                        });
                     });
                  }
               }

               // Accumulate chunk in ref
               const beforeLength = responseBufferRef.current.length;
               responseBufferRef.current += chunk;
               const afterLength = responseBufferRef.current.length;
               console.log(
                  '[ChatWidget] Buffer: before=',
                  beforeLength,
                  'after=',
                  afterLength,
                  'added=',
                  chunk.length,
               );

               // Schedule update immediately
               const updateStartTime = performance.now();
               scheduleUpdate();
               const updateDuration = performance.now() - updateStartTime;
               console.log(
                  '[ChatWidget] scheduleUpdate() called, took',
                  updateDuration.toFixed(2),
                  'ms',
               );
            },
            (status) => {
               console.log('[ChatWidget] STATUS received:', status);
               // Update status in the assistant message
               if (assistantMessageIdRef.current) {
                  setMessages((prev) => {
                     return prev.map((msg) => {
                        if (msg.id === assistantMessageIdRef.current) {
                           return {
                              ...msg,
                              status: status,
                           };
                        }
                        return msg;
                     });
                  });
               }
            },
            (thinking) => {
               console.log('[ChatWidget] THINKING received, length:', thinking.length);
               // Update status to show reasoning
               if (assistantMessageIdRef.current) {
                  setMessages((prev) => {
                     return prev.map((msg) => {
                        if (msg.id === assistantMessageIdRef.current) {
                           return {
                              ...msg,
                              status: 'Reasoning...',
                           };
                        }
                        return msg;
                     });
                  });
               }
               // Accumulate thinking chunks in ref
               thinkingBufferRef.current += thinking;
               // Schedule update
               scheduleUpdate();
            },
         );

         // Final update to ensure all content is displayed
         if (assistantMessageIdRef.current) {
            setMessages((prev) => {
               return prev.map((msg) => {
                  if (msg.id === assistantMessageIdRef.current) {
                     return {
                        ...msg,
                        content: responseBufferRef.current,
                        thinking: thinkingBufferRef.current || msg.thinking,
                        status: responseBufferRef.current.length > 0 ? undefined : msg.status, // Clear status if we have content
                     };
                  }
                  return msg;
               });
            });
         }
      } catch (error) {
         console.error('Chat error:', error);
         setIsTyping(false);
         const errorMessage: Message = {
            id: (Date.now() + 1).toString(),
            role: 'assistant',
            content:
               "Sorry, I couldn't reach the financial advisor service. Please check if the backend is running.",
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

   // Close dropdown when clicking outside
   useEffect(() => {
      const handleClickOutside = (event: MouseEvent) => {
         if (
            dropdownRef.current &&
            !dropdownRef.current.contains(event.target as Node) &&
            !infoModalRef.current?.contains(event.target as Node)
         ) {
            setIsModelDropdownOpen(false);
         }
      };

      if (isModelDropdownOpen) {
         document.addEventListener('mousedown', handleClickOutside);
      }

      return () => {
         document.removeEventListener('mousedown', handleClickOutside);
      };
   }, [isModelDropdownOpen]);

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
                     className="flex-1 px-6 py-4 bg-white dark:bg-zinc-900 border-2 border-zinc-200 dark:border-zinc-800 rounded-full shadow-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-zinc-900 text-zinc-900 dark:text-zinc-100 placeholder-zinc-500"
                  />
                  <button
                     type="submit"
                     className="bg-blue-600 text-white p-4 rounded-full shadow-lg hover:bg-blue-700 hover:scale-110 active:scale-95 transition-all duration-200 flex items-center justify-center disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100"
                     disabled={!inputValue.trim()}>
                     <Send className="size-5" />
                  </button>
               </form>
            </div>
         )}

         {/* Expanded State - Full Chat Interface */}
         {isExpanded && (
            <>
               <div className="fixed bottom-0 left-1/2 -translate-x-1/2 w-full max-w-3xl bg-white dark:bg-zinc-900 border-x border-t rounded-t-2xl shadow-2xl flex flex-col h-[600px] z-50 animate-in slide-in-from-bottom duration-500 ease-out">
                  {/* Header */}
                  <div className="p-4 border-b bg-gradient-to-r from-blue-50 to-indigo-50 dark:from-zinc-800 dark:to-zinc-900 rounded-t-2xl flex justify-between items-center">
                     <div className="flex items-center gap-3">
                        <div className="size-10 bg-blue-600 rounded-full flex items-center justify-center">
                           <span className="text-white font-bold text-sm">AI</span>
                        </div>
                        <div>
                           <h3 className="font-semibold text-zinc-900 dark:text-zinc-100">
                              AI Financial Assistant
                           </h3>
                           <span className="text-xs text-zinc-500 flex items-center gap-1">
                              <span className="size-2 bg-green-500 rounded-full"></span>
                              Connected
                           </span>
                        </div>
                     </div>
                     <button
                        onClick={() => setIsExpanded(false)}
                        className="p-2 hover:bg-zinc-100 dark:hover:bg-zinc-800 hover:scale-110 active:scale-95 rounded-full transition-all duration-200"
                        aria-label="Minimize chat">
                        <Minimize2 className="size-5 text-zinc-600 dark:text-zinc-400" />
                     </button>
                  </div>

                  {/* Messages Area */}
                  <div className="flex-1 p-4 overflow-y-auto space-y-4 bg-zinc-50/50 dark:bg-zinc-950/50">
                     {messages.map((message) => (
                        <div
                           key={message.id}
                           className={`flex ${
                              message.role === 'user' ? 'justify-end' : 'justify-start'
                           }`}>
                           <div
                              className={`max-w-[80%] p-3 rounded-2xl ${
                                 message.role === 'user'
                                    ? 'bg-blue-600 text-white rounded-tr-none'
                                    : 'bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 rounded-tl-none shadow-sm'
                              }`}>
                              {message.role === 'assistant' ? (
                                 <>
                                    {/* Show status if no content yet */}
                                    {message.status && !message.content && (
                                       <div className="flex items-center gap-2 text-sm text-zinc-600 dark:text-zinc-400">
                                          <div className="flex gap-1">
                                             <div className="size-1.5 bg-zinc-400 rounded-full animate-bounce [animation-delay:-0.3s]"></div>
                                             <div className="size-1.5 bg-zinc-400 rounded-full animate-bounce [animation-delay:-0.15s]"></div>
                                             <div className="size-1.5 bg-zinc-400 rounded-full animate-bounce"></div>
                                          </div>
                                          <span>{message.status}</span>
                                       </div>
                                    )}
                                    {/* Chain-of-Thought Reasoning */}
                                    {message.thinking && !message.content && (
                                       <div className="">
                                          <div
                                             className="text-xs text-zinc-600 dark:text-zinc-500 leading-relaxed whitespace-pre-wrap font-mono mt-1"
                                             dangerouslySetInnerHTML={{
                                                __html: DOMPurify ? DOMPurify.sanitize(message.thinking, {
                                                   ALLOWED_TAGS: [],
                                                   ALLOWED_ATTR: [],
                                                   KEEP_CONTENT: true,
                                                }) : message.thinking,
                                             }}
                                          />
                                       </div>
                                    )}
                                    {/* Main Response - Show content if available */}
                                    {message.content ? (
                                       <OptimizedMarkdown content={message.content} />
                                    ) : null}
                                 </>
                              ) : (
                                 <p className="text-sm leading-relaxed whitespace-pre-wrap">
                                    {DOMPurify ? DOMPurify.sanitize(message.content, { ALLOWED_TAGS: [] }) : message.content}
                                 </p>
                              )}
                              <p className="text-xs mt-2 opacity-70">
                                 {message.timestamp.toLocaleTimeString([], {
                                    hour: '2-digit',
                                    minute: '2-digit',
                                 })}
                              </p>
                           </div>
                        </div>
                     ))}
                     <div ref={messagesEndRef} />
                  </div>

                  {/* Input Area */}
                  <div className="p-4 border-t bg-white dark:bg-zinc-900 rounded-b-2xl relative">
                     <form onSubmit={handleSubmit} className="flex gap-2">
                        <input
                           ref={inputRef}
                           type="text"
                           placeholder="Ask me anything..."
                           value={inputValue}
                           onChange={(e) => setInputValue(e.target.value)}
                           className="flex-1 px-4 py-3 border-2 border-zinc-200 dark:border-zinc-700 rounded-full focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:bg-zinc-950 dark:text-zinc-100 placeholder-zinc-600 transition-all"
                           disabled={isTyping}
                        />
                        <div className="relative flex items-center" ref={dropdownRef}>
                           <button
                              type="button"
                              onClick={() => setIsModelDropdownOpen(!isModelDropdownOpen)}
                              className="text-xs text-zinc-900 dark:text-zinc-500 hover:scale-105 active:scale-100 transition-all duration-200 flex items-center gap-1 px-2 py-2 rounded-lg hover:bg-zinc-100 dark:hover:bg-zinc-800">
                              {isSupernovaEnabled ? 'Supernova' : 'default'}
                              <ChevronDown
                                 className={`size-3 transition-transform duration-200 ${
                                    isModelDropdownOpen ? 'rotate-180' : ''
                                 }`}
                              />
                           </button>

                           {/* Dropdown Menu */}
                           {isModelDropdownOpen && (
                              <div className="absolute bottom-full right-0 mb-2 w-64 bg-white dark:bg-zinc-800 rounded-xl shadow-2xl border border-zinc-200 dark:border-zinc-700 p-3 animate-in fade-in slide-in-from-bottom-2 duration-200 z-50">
                                 <div className="flex items-center justify-between mb-1.5">
                                    <span className="text-sm font-medium text-zinc-900 dark:text-zinc-400">
                                       Model Selection
                                    </span>
                                    <button
                                       onClick={() => {
                                          setIsInfoModalOpen(true);
                                       }}
                                       className="p-1.5 hover:bg-zinc-100 dark:hover:bg-zinc-700 rounded-lg transition-colors"
                                       aria-label="Model information">
                                       <Info className="size-4 text-zinc-500 dark:text-zinc-400" />
                                    </button>
                                 </div>

                                 <div className="flex items-center justify-between p-3 bg-zinc-50 dark:bg-zinc-900/50 rounded-lg border border-zinc-200 dark:border-zinc-700">
                                    <div className="flex items-center gap-2">
                                       {isSupernovaEnabled ? (
                                          <Sparkles className="size-4 text-amber-500" />
                                       ) : (
                                          <Zap className="size-4 text-blue-500" />
                                       )}
                                       <div>
                                          <div className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
                                             {isSupernovaEnabled ? 'Supernova' : 'Default'}
                                          </div>
                                          <div className="text-xs text-zinc-500 dark:text-zinc-400">
                                             {isSupernovaEnabled
                                                ? 'Advanced reasoning'
                                                : 'Fast & efficient'}
                                          </div>
                                       </div>
                                    </div>
                                    <div
                                       onClick={() => setIsSupernovaEnabled(!isSupernovaEnabled)}
                                       className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                                          isSupernovaEnabled ? 'bg-amber-500' : 'bg-blue-600'
                                       }`}>
                                       <span
                                          className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                                             isSupernovaEnabled ? 'translate-x-6' : 'translate-x-1'
                                          }`}
                                       />
                                    </div>
                                 </div>
                              </div>
                           )}
                        </div>
                        <button
                           type="submit"
                           className="bg-blue-600 text-white px-6 py-3 rounded-full font-medium hover:bg-blue-700 hover:scale-105 active:scale-95 transition-all duration-200 flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100"
                           disabled={!inputValue.trim() || isTyping}>
                           <Send className="size-4" />
                           Send
                        </button>
                     </form>
                  </div>
               </div>
               {/* Info Modal */}
               {isInfoModalOpen && (
                  <div
                     className="fixed inset-0 bg-black/50 backdrop-blur-sm z-[60] flex items-center justify-center p-4 animate-in fade-in duration-200"
                     ref={infoModalRef}
                     onClick={() => {
                        setIsInfoModalOpen(false);
                     }}>
                     <div
                        className="bg-white dark:bg-zinc-900 rounded-2xl shadow-2xl w-full max-w-lg p-8 animate-in zoom-in-95 duration-200 relative overflow-hidden"
                        onClick={(e) => e.stopPropagation()}>
                        <div className="absolute top-0 right-0 p-4">
                           <button
                              onClick={() => {
                                 setIsInfoModalOpen(false);
                              }}
                              className="p-2 hover:bg-zinc-100 dark:hover:bg-zinc-800 rounded-full transition-colors">
                              <X className="size-5" />
                           </button>
                        </div>

                        <div className="flex flex-col items-center text-center mb-6">
                           <div className="size-16 bg-gradient-to-br from-blue-500 to-indigo-600 rounded-2xl flex items-center justify-center shadow-xl shadow-blue-500/20 mb-3 p-4">
                              <Sparkles className="size-8 text-white" />
                           </div>
                           <h2 className="text-2xl font-bold mb-1 text-zinc-900 dark:text-zinc-100">
                              Model Selection
                           </h2>
                           <p className="text-zinc-500 text-sm">
                              Choose the right model for your needs
                           </p>
                        </div>

                        <div className="space-y-4">
                           <div className="p-4 bg-zinc-50 dark:bg-zinc-800/50 rounded-xl border border-zinc-200 dark:border-zinc-700">
                              <div className="flex items-start gap-3">
                                 <Zap className="size-5 text-blue-500 mt-0.5 flex-shrink-0" />
                                 <div>
                                    <h3 className="font-semibold mb-1 text-zinc-900 dark:text-zinc-100">
                                       Default Model
                                    </h3>
                                    <p className="text-sm text-zinc-600 dark:text-zinc-400 leading-relaxed">
                                       Fast and efficient, perfect for quick answers and simple
                                       queries. Provides instant responses with balanced quality and
                                       speed.
                                    </p>
                                 </div>
                              </div>
                           </div>

                           <div className="relative p-4 bg-amber-50 dark:bg-amber-900/10 rounded-xl border border-amber-200 dark:border-amber-800 overflow-hidden">
                              <div className="absolute bottom-0 right-0 h-[88%] opacity-12 grayscale">
                                 <Image
                                    width={100}
                                    height={100}
                                    src="/supernova.png"
                                    alt="Supernova"
                                    className="w-full h-full object-contain"
                                    draggable={false}
                                    unselectable="on"
                                 />
                              </div>
                              <div className="flex items-start gap-3">
                                 <Sparkles className="size-5 text-amber-500 mt-0.5 flex-shrink-0" />
                                 <div>
                                    <h3 className="font-semibold mb-1 text-zinc-900 dark:text-zinc-100">
                                       Supernova
                                    </h3>
                                    <p className="text-sm text-zinc-600 dark:text-zinc-400 leading-relaxed">
                                       Advanced reasoning model that provides chain-of-thought
                                       analysis, gathers more comprehensive data, and delivers
                                       deeper insights. Best for complex financial questions and
                                       detailed analysis.
                                    </p>
                                 </div>
                              </div>
                           </div>

                           <button
                              onClick={() => {
                                 setIsInfoModalOpen(false);
                              }}
                              className="w-full py-3 bg-blue-600 hover:bg-blue-700 text-white rounded-xl font-bold text-base shadow-lg shadow-blue-500/25 transition-all">
                              Got it
                           </button>
                        </div>
                     </div>
                  </div>
               )}
            </>
         )}
      </>
   );
}

// Optimized markdown component - updates immediately for streaming
function OptimizedMarkdown({ content }: { content: string }) {
   // Log when component receives new content
   useEffect(() => {
      console.log('[OptimizedMarkdown] Content updated, length:', content.length);
      console.log('[OptimizedMarkdown] Content preview:', content.substring(0, 100));
   }, [content]);

   // Update immediately - no debouncing for real-time streaming experience
   // ReactMarkdown will re-parse on each update, but that's acceptable for streaming UX

   // Sanitize content (defense in depth - react-markdown already sanitizes)
   // Don't use useMemo here - we want immediate updates during streaming
   const sanitizedContent = DOMPurify ? DOMPurify.sanitize(content, {
      ALLOWED_TAGS: [
         'p',
         'strong',
         'em',
         'code',
         'pre',
         'ul',
         'ol',
         'li',
         'h1',
         'h2',
         'h3',
         'h4',
         'h5',
         'h6',
         'a',
         'blockquote',
         'hr',
         'table',
         'thead',
         'tbody',
         'tr',
         'th',
         'td',
         'br',
      ],
      ALLOWED_ATTR: ['href', 'target', 'rel', 'className'],
      ALLOW_DATA_ATTR: false,
   }) : content;

   return (
      <div className="markdown-content text-sm leading-relaxed">
         <ReactMarkdown
            remarkPlugins={[remarkGfm]}
            rehypePlugins={[rehypeRaw]}
            components={{
               // Customize code blocks
               // eslint-disable-next-line @typescript-eslint/no-explicit-any
               code: ({ className, children, ...props }: any) => {
                  const match = /language-(\w+)/.exec(className || '');
                  const isInline = !className || !match;
                  return !isInline && match ? (
                     <pre className="bg-zinc-100 dark:bg-zinc-900 rounded-lg p-3 overflow-x-auto my-2 text-xs">
                        <code className={className} {...props}>
                           {children}
                        </code>
                     </pre>
                  ) : (
                     <code
                        className="bg-zinc-100 dark:bg-zinc-900 px-1.5 py-0.5 rounded text-xs font-mono"
                        {...props}>
                        {children}
                     </code>
                  );
               },
               // Customize paragraphs
               p: ({ children }) => <p className="mb-2 last:mb-0">{children}</p>,
               // Customize lists
               ul: ({ children }) => (
                  <ul className="list-disc mb-2 space-y-1.5 ml-6 pl-0">{children}</ul>
               ),
               ol: ({ children }) => (
                  <ol className="list-decimal mb-2 space-y-1.5 ml-6 pl-0">{children}</ol>
               ),
               li: ({ children, ...props }) => (
                  <li
                     className="mb-1.5 leading-relaxed [&>p]:inline [&>p]:m-0"
                     style={{
                        display: 'list-item',
                        listStylePosition: 'outside',
                     }}
                     {...props}>
                     {children}
                  </li>
               ),
               // Customize headings
               h1: ({ children }) => (
                  <h1 className="text-lg font-bold mb-2 mt-3 first:mt-0">{children}</h1>
               ),
               h2: ({ children }) => (
                  <h2 className="text-base font-semibold mb-2 mt-3 first:mt-0">{children}</h2>
               ),
               h3: ({ children }) => (
                  <h3 className="text-sm font-semibold mb-1 mt-2 first:mt-0">{children}</h3>
               ),
               // Customize links
               a: ({ href, children }) => (
                  <a
                     href={href}
                     target="_blank"
                     rel="noopener noreferrer"
                     className="text-blue-600 dark:text-blue-400 hover:underline">
                     {children}
                  </a>
               ),
               // Customize blockquotes
               blockquote: ({ children }) => (
                  <blockquote className="border-l-4 border-zinc-300 dark:border-zinc-600 pl-4 italic my-2 text-zinc-600 dark:text-zinc-400">
                     {children}
                  </blockquote>
               ),
               // Customize strong/bold
               strong: ({ children }) => <strong className="font-semibold">{children}</strong>,
               // Customize emphasis/italic
               em: ({ children }) => <em className="italic">{children}</em>,
               // Customize horizontal rule
               hr: () => <hr className="my-3 border-zinc-300 dark:border-zinc-600" />,
               // Customize tables
               table: ({ children }) => (
                  <div className="overflow-x-auto my-2">
                     <table className="min-w-full border-collapse border border-zinc-300 dark:border-zinc-600">
                        {children}
                     </table>
                  </div>
               ),
               th: ({ children }) => (
                  <th className="border border-zinc-300 dark:border-zinc-600 px-3 py-2 bg-zinc-100 dark:bg-zinc-800 font-semibold text-left">
                     {children}
                  </th>
               ),
               td: ({ children }) => (
                  <td className="border border-zinc-300 dark:border-zinc-600 px-3 py-2">
                     {children}
                  </td>
               ),
            }}>
            {sanitizedContent}
         </ReactMarkdown>
      </div>
   );
}
