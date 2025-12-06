'use client';

import React, { useState, useEffect, useRef } from 'react';
import { X, TrendingUp, AlertCircle, DollarSign, BarChart3, ArrowRight, ChevronLeft, ChevronRight, Newspaper, Loader2 } from 'lucide-react';
import { useUI } from '../context/UIContext';
import { api } from '@/lib/api';

interface NewsItem {
  id: string;
  type: 'market' | 'portfolio' | 'alert' | 'insight';
  title: string;
  description: string;
  fullContent: string;
  timestamp: string;
  source: string;
  icon: React.ReactNode;
}

interface ApiNewsItem {
  id: string;
  title: string;
  summary: string;
  content: string; // Full content from backend
  source: string;
  publishedAt: string;
}

// Helper to determine news type based on content
const getNewsType = (title: string, source: string): 'market' | 'portfolio' | 'alert' | 'insight' => {
  const lowerTitle = title.toLowerCase();
  if (lowerTitle.includes('alert') || lowerTitle.includes('warning') || lowerTitle.includes('risk')) {
    return 'alert';
  }
  if (lowerTitle.includes('portfolio') || lowerTitle.includes('your')) {
    return 'portfolio';
  }
  if (lowerTitle.includes('analysis') || lowerTitle.includes('insight') || lowerTitle.includes('outlook')) {
    return 'insight';
  }
  return 'market';
};

// Helper to get time ago string
const getTimeAgo = (dateString: string): string => {
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins} minute${diffMins > 1 ? 's' : ''} ago`;
  if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
  return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
};

// Helper to get icon based on news type
const getNewsIcon = (type: string) => {
  switch (type) {
    case 'alert': return <AlertCircle className="size-5" />;
    case 'portfolio': return <DollarSign className="size-5" />;
    case 'insight': return <BarChart3 className="size-5" />;
    default: return <TrendingUp className="size-5" />;
  }
};

const SCROLL_INTERVAL = 4000; // 4 seconds

export function WhatsNewPanel() {
  const { setModalOpen } = useUI();
  const [newsItems, setNewsItems] = useState<NewsItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [selectedItem, setSelectedItem] = useState<NewsItem | null>(null);
  const [isPaused, setIsPaused] = useState(false);
  const intervalRef = useRef<NodeJS.Timeout | null>(null);

  // Fetch news from API on mount
  useEffect(() => {
    const fetchNews = async () => {
      try {
        const data = await api.dashboard.getNews() as ApiNewsItem[];
        const transformedNews: NewsItem[] = data.map((item) => {
          const type = getNewsType(item.title, item.source);
          return {
            id: item.id,
            type,
            title: item.title,
            description: item.summary,
            fullContent: item.content || item.summary, // Use full content from API
            timestamp: getTimeAgo(item.publishedAt),
            source: item.source,
            icon: getNewsIcon(type),
          };
        });
        setNewsItems(transformedNews);
      } catch (error) {
        console.error('Failed to fetch news:', error);
        // Set fallback news on error
        setNewsItems([{
          id: 'fallback',
          type: 'market',
          title: 'Market Update',
          description: 'Unable to load latest news. Please try again later.',
          fullContent: 'Unable to load latest news. Please try again later.',
          timestamp: 'Now',
          source: 'System',
          icon: <Newspaper className="size-5" />,
        }]);
      } finally {
        setIsLoading(false);
      }
    };

    fetchNews();
    // Refresh news every 5 minutes
    const refreshInterval = setInterval(fetchNews, 5 * 60 * 1000);
    return () => clearInterval(refreshInterval);
  }, []);

  useEffect(() => {
    if (!isPaused && !selectedItem && newsItems.length > 0) {
      intervalRef.current = setInterval(() => {
        setCurrentIndex((prev) => (prev + 1) % newsItems.length);
      }, SCROLL_INTERVAL);
    }

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    };
  }, [isPaused, selectedItem, newsItems.length]);

  const resetInterval = () => {
    if (intervalRef.current) clearInterval(intervalRef.current);
    intervalRef.current = setInterval(() => {
      setCurrentIndex((prev) => (prev + 1) % newsItems.length);
    }, SCROLL_INTERVAL);
  };

  const handleItemClick = (item: NewsItem) => {
    setSelectedItem(item);
    setIsPaused(true);
    setModalOpen(true);
    if (intervalRef.current) clearInterval(intervalRef.current);
  };

  const handleCloseModal = () => {
    setSelectedItem(null);
    setIsPaused(false);
    setModalOpen(false);
  };

  const handleNext = () => {
    setCurrentIndex((prev) => (prev + 1) % newsItems.length);
    resetInterval();
  };

  const handlePrevious = () => {
    setCurrentIndex((prev) => (prev - 1 + newsItems.length) % newsItems.length);
    resetInterval();
  };

  const jumpToSlide = (index: number) => {
    setCurrentIndex(index);
    resetInterval();
  };

  const currentItem = newsItems.length > 0 ? newsItems[currentIndex] : null;

  const getTypeColor = (type: string) => {
    switch (type) {
      case 'market':
        return 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400';
      case 'portfolio':
        return 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400';
      case 'alert':
        return 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400';
      case 'insight':
        return 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400';
      default:
        return 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-400';
    }
  };

  return (
    <>
      <div className="bg-white dark:bg-zinc-900 rounded-xl border p-6 h-full flex flex-col overflow-hidden">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-bold">What's New for You</h2>
        </div>
        
        <div className="flex-1 relative overflow-hidden rounded-xl bg-gradient-to-br from-zinc-50 to-zinc-100 dark:from-zinc-800 dark:to-zinc-900 border border-zinc-200 dark:border-zinc-700">
          {isLoading ? (
            <div className="absolute inset-0 flex items-center justify-center">
              <Loader2 className="size-8 animate-spin text-blue-500" />
            </div>
          ) : currentItem ? (
            <>
              <div
                key={currentItem.id}
                className="absolute inset-0 animate-slide-up cursor-pointer group p-5 flex flex-col pb-14"
                onClick={() => handleItemClick(currentItem)}
              >
                <div className="flex items-center gap-2 mb-2">
                  <div className={`p-1.5 rounded-lg ${getTypeColor(currentItem.type)}`}>
                    {currentItem.icon}
                  </div>
                  <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${getTypeColor(currentItem.type)}`}>
                    {currentItem.type.charAt(0).toUpperCase() + currentItem.type.slice(1)}
                  </span>
                  <span className="text-xs text-zinc-500 ml-auto">{currentItem.timestamp}</span>
                </div>
                <h3 className="font-semibold text-base mb-1.5 line-clamp-2">{currentItem.title}</h3>
                <p className="text-sm text-zinc-600 dark:text-zinc-400 line-clamp-4 mb-2 flex-1">{currentItem.description}</p>
                <div className="flex items-center justify-between">
                  <p className="text-xs text-zinc-400 dark:text-zinc-500">Source: {currentItem.source}</p>
                  <div className="flex items-center gap-1 text-blue-600 dark:text-blue-400 text-xs font-medium group-hover:gap-2 transition-all">
                    Read more <ArrowRight className="size-3" />
                  </div>
                </div>
              </div>

              {/* Integrated Navigation Overlay - Horizontal Bottom */}
              <div className="absolute bottom-3 left-1/2 -translate-x-1/2 flex items-center gap-2 px-3 py-1.5 rounded-full transition-all duration-300 border border-transparent hover:bg-white/50 hover:dark:bg-black/20 hover:backdrop-blur-sm hover:border-zinc-200/50 hover:dark:border-zinc-700/50 pointer-events-auto">
                <button 
                  onClick={(e) => { e.stopPropagation(); handlePrevious(); }}
                  className="p-1 rounded-full transition-colors text-zinc-300 hover:text-zinc-600 dark:text-zinc-600 dark:hover:text-zinc-200"
                >
                  <ChevronLeft className="size-4" />
                </button>
                
                <div className="flex gap-1.5 px-1">
                  {newsItems.map((_, idx) => (
                    <button
                      key={idx}
                      onClick={(e) => {
                        e.stopPropagation();
                        jumpToSlide(idx);
                      }}
                      className={`h-1.5 rounded-full transition-all duration-300 ${
                        idx === currentIndex
                          ? 'bg-blue-600 w-6'
                          : 'bg-zinc-300 dark:bg-zinc-600 w-1.5 hover:bg-blue-400'
                      }`}
                      aria-label={`Go to slide ${idx + 1}`}
                    />
                  ))}
                </div>

                <button 
                  onClick={(e) => { e.stopPropagation(); handleNext(); }}
                  className="p-1 rounded-full transition-colors text-zinc-300 hover:text-zinc-600 dark:text-zinc-600 dark:hover:text-zinc-200"
                >
                  <ChevronRight className="size-4" />
                </button>
              </div>
            </>
          ) : (
            <div className="absolute inset-0 flex items-center justify-center text-zinc-500">
              No news available
            </div>
          )}
        </div>
      </div>

      {/* Expanded Modal */}
      {selectedItem && (
        <div
          className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-in fade-in duration-200"
          onClick={handleCloseModal}
        >
          <div
            className="bg-white dark:bg-zinc-900 rounded-2xl shadow-2xl max-w-2xl w-full max-h-[80vh] overflow-y-auto animate-in zoom-in-95 duration-200"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="sticky top-0 bg-white dark:bg-zinc-900 border-b p-4 flex items-center justify-between rounded-t-2xl">
              <div className="flex items-center gap-3">
                <div className={`p-2 rounded-lg ${getTypeColor(selectedItem.type)}`}>
                  {selectedItem.icon}
                </div>
                <div>
                  <h2 className="font-bold text-xl">{selectedItem.title}</h2>
                  <p className="text-sm text-zinc-500">{selectedItem.timestamp}</p>
                </div>
              </div>
              <button
                onClick={handleCloseModal}
                className="p-2 hover:bg-zinc-100 dark:hover:bg-zinc-800 rounded-full transition-colors"
                aria-label="Close"
              >
                <X className="size-5" />
              </button>
            </div>
            <div className="p-6">
              <div className="flex items-center gap-2 mb-4">
                <span className={`inline-block px-3 py-1 rounded-full text-sm font-medium ${getTypeColor(selectedItem.type)}`}>
                  {selectedItem.type.charAt(0).toUpperCase() + selectedItem.type.slice(1)}
                </span>
                <span className="text-sm text-zinc-500">via {selectedItem.source}</span>
              </div>
              <p className="text-zinc-700 dark:text-zinc-300 leading-relaxed whitespace-pre-line">
                {selectedItem.fullContent}
              </p>
            </div>
          </div>
        </div>
      )}

      <style jsx>{`
        @keyframes slide-up {
          from {
            opacity: 0;
            transform: translateY(20px);
          }
          to {
            opacity: 1;
            transform: translateY(0);
          }
        }
        .animate-slide-up {
          animation: slide-up 0.5s ease-out;
        }
      `}</style>
    </>
  );
}
