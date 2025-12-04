'use client';

import React, { useState, useEffect, useRef } from 'react';
import { X, TrendingUp, AlertCircle, DollarSign, BarChart3, ArrowRight, ChevronLeft, ChevronRight } from 'lucide-react';
import { useUI } from '../context/UIContext';

interface NewsItem {
  id: string;
  type: 'market' | 'portfolio' | 'alert' | 'insight';
  title: string;
  description: string;
  fullContent: string;
  timestamp: string;
  icon: React.ReactNode;
}

const newsItems: NewsItem[] = [
  {
    id: '1',
    type: 'market',
    title: 'Tech Sector Momentum',
    description: 'Tech sector shows strong momentum today with major indices up 1.5%.',
    fullContent: 'The technology sector is experiencing significant momentum today, with the NASDAQ up 1.5% and major tech stocks leading the gains. Apple (AAPL) and Microsoft (MSFT) are both up over 2%, while NVIDIA (NVDA) continues its strong performance with a 3% gain. Analysts attribute this to positive earnings expectations and strong consumer demand for tech products.',
    timestamp: '2 minutes ago',
    icon: <TrendingUp className="size-5" />,
  },
  {
    id: '2',
    type: 'portfolio',
    title: 'Portfolio Alert: NVDA',
    description: 'Your simulated position in NVDA is up 2.5% today.',
    fullContent: 'Your simulated position in NVIDIA (NVDA) has increased by 2.5% today, adding approximately $60.69 to your portfolio value. The stock is currently trading at $485.50, up from yesterday\'s close. This represents a strong performance for your 5-share position, which is now valued at $2,427.50.',
    timestamp: '15 minutes ago',
    icon: <DollarSign className="size-5" />,
  },
  {
    id: '3',
    type: 'alert',
    title: 'Market Volatility Warning',
    description: 'Increased volatility detected in the energy sector.',
    fullContent: 'Market analysts are reporting increased volatility in the energy sector today, with oil prices fluctuating significantly. This may impact related stocks and ETFs. Consider reviewing your energy-related positions and adjusting your risk management strategies accordingly.',
    timestamp: '1 hour ago',
    icon: <AlertCircle className="size-5" />,
  },
  {
    id: '4',
    type: 'insight',
    title: 'Portfolio Diversification',
    description: 'Your portfolio shows good diversification across tech stocks.',
    fullContent: 'Your current portfolio demonstrates strong diversification across technology stocks, with positions in Apple, Microsoft, NVIDIA, Alphabet, and Tesla. This diversification helps mitigate risk while maintaining exposure to the high-growth technology sector. Consider reviewing your allocation percentages to ensure optimal risk-return balance.',
    timestamp: '2 hours ago',
    icon: <BarChart3 className="size-5" />,
  },
  {
    id: '5',
    type: 'market',
    title: 'Earnings Season Update',
    description: 'Upcoming earnings reports may impact your holdings.',
    fullContent: 'Several companies in your portfolio are approaching their earnings announcement dates. Apple and Microsoft are scheduled to report next week, which could significantly impact their stock prices. Historical data shows that these companies tend to experience increased volatility around earnings announcements. Consider monitoring these positions closely.',
    timestamp: '3 hours ago',
    icon: <TrendingUp className="size-5" />,
  },
];

const SCROLL_INTERVAL = 4000; // 4 seconds

export function WhatsNewPanel() {
  const { setModalOpen } = useUI();
  const [currentIndex, setCurrentIndex] = useState(0);
  const [selectedItem, setSelectedItem] = useState<NewsItem | null>(null);
  const [isPaused, setIsPaused] = useState(false);
  const intervalRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    if (!isPaused && !selectedItem) {
      intervalRef.current = setInterval(() => {
        setCurrentIndex((prev) => (prev + 1) % newsItems.length);
      }, SCROLL_INTERVAL);
    }

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    };
  }, [isPaused, selectedItem]);

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

  const currentItem = newsItems[currentIndex];

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
            <div
              key={currentItem.id}
              className="absolute inset-0 animate-slide-up cursor-pointer group p-6 flex flex-col justify-between pb-12"
              onClick={() => handleItemClick(currentItem)}
            >
            <div>
              <div className="flex items-center gap-2 mb-3">
                <div className={`p-2 rounded-lg ${getTypeColor(currentItem.type)}`}>
                  {currentItem.icon}
                </div>
                <span className={`text-xs px-2 py-1 rounded-full font-medium ${getTypeColor(currentItem.type)}`}>
                  {currentItem.type.charAt(0).toUpperCase() + currentItem.type.slice(1)}
                </span>
                <span className="text-xs text-zinc-500 ml-auto">{currentItem.timestamp}</span>
              </div>
              <h3 className="font-semibold text-lg mb-2">{currentItem.title}</h3>
              <p className="text-sm text-zinc-600 dark:text-zinc-400 line-clamp-3 mb-4">{currentItem.description}</p>
              
              <div className="flex items-center gap-1 text-blue-600 dark:text-blue-400 text-sm font-medium mt-auto group-hover:gap-2 transition-all">
                Read full update <ArrowRight className="size-4" />
              </div>
            </div>
          </div>

          {/* Integrated Navigation Overlay - Horizontal Bottom */}
          <div className="absolute bottom-3 left-1/2 -translate-x-1/2 flex items-center gap-2 px-3 py-1.5 rounded-full transition-all duration-300 border border-transparent hover:bg-white/50 hover:dark:bg-black/20 hover:backdrop-blur-sm hover:border-zinc-200/50 hover:dark:border-zinc-700/50 z-10">
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
              <div className={`inline-block px-3 py-1 rounded-full text-sm font-medium mb-4 ${getTypeColor(selectedItem.type)}`}>
                {selectedItem.type.charAt(0).toUpperCase() + selectedItem.type.slice(1)}
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
