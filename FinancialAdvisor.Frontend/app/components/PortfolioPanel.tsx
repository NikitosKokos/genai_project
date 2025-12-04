'use client';

import React, { useState, useEffect } from 'react';
import { LineChart, Line, AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { TrendingUp, TrendingDown, X, Brain, Newspaper, Activity, ChevronRight, Plus, Wallet, Loader2, CheckCircle2 } from 'lucide-react';
import { useUI } from '../context/UIContext';

// Generate dummy portfolio data based on time range
const generatePortfolioData = (range: string = '1M') => {
  const data = [];
  let baseValue = 100000;
  let points = 30;
  let volatility = 2000;
  let trend = 1; // 1 = up, -1 = down, 0 = flat
  let dateStep = 1; // Days
  let dateFormat: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };
  
  switch(range) {
    case '1D': 
      points = 24; 
      volatility = 200; 
      trend = 0.2; 
      baseValue = 112500;
      dateStep = 1/24; // Hourly roughly
      dateFormat = { hour: 'numeric' };
      break;
    case '1W': 
      points = 7; 
      volatility = 800; 
      trend = 0.5;
      baseValue = 111000;
      dateStep = 1;
      break;
    case '1M': 
      points = 30; 
      volatility = 2000; 
      trend = 1;
      baseValue = 100000;
      dateStep = 1;
      break;
    case '3M': 
      points = 90; 
      volatility = 3000; 
      trend = 1.2;
      baseValue = 90000;
      dateStep = 1;
      break;
    case 'YTD': 
      points = 150; 
      volatility = 4000; 
      trend = 1.5;
      baseValue = 85000;
      dateStep = 2;
      break;
    case '1Y': 
      points = 12; 
      volatility = 5000; 
      trend = 2;
      baseValue = 80000;
      dateStep = 30;
      dateFormat = { month: 'short' };
      break;
    case 'ALL': 
      points = 50; 
      volatility = 8000; 
      trend = 3;
      baseValue = 50000;
      dateStep = 30;
      dateFormat = { month: 'short', year: '2-digit' };
      break;
  }
  
  let currentValue = baseValue;
  const now = new Date();
  const startDate = new Date(now);
  startDate.setDate(now.getDate() - (points * dateStep));
  
  for (let i = 0; i < points; i++) {
    const change = (Math.random() - 0.4 + (trend * 0.05)) * volatility; 
    currentValue += change;
    
    const date = new Date(startDate);
    if (range === '1D') {
      date.setHours(startDate.getHours() + i);
    } else {
      date.setDate(startDate.getDate() + (i * dateStep));
    }

    data.push({
      date: date.toLocaleDateString('en-US', dateFormat),
      value: Math.round(currentValue),
    });
  }
  return data;
};

// Generate sparkline data for individual positions
const generateSparklineData = (trend: 'up' | 'down' = 'up') => {
  const data = [];
  const base = 100;
  let current = base;
  
  for (let i = 0; i < 20; i++) {
    const change = trend === 'up' 
      ? (Math.random() - 0.3) * 5 
      : (Math.random() - 0.7) * 5;
    current += change;
    data.push({ value: Math.max(50, Math.min(150, current)) });
  }
  return data;
};

const CustomTooltip = ({ active, payload, label }: any) => {
  if (active && payload && payload.length) {
    return (
      <div className="bg-zinc-900/90 backdrop-blur-sm border border-zinc-700 p-2 rounded-lg shadow-xl">
        <p className="text-zinc-100 text-xs font-mono font-medium">
          ${payload[0].value.toFixed(3)}
        </p>
      </div>
    );
  }
  return null;
};

export function PortfolioPanel() {
  const { setModalOpen } = useUI();
  const [isHovered, setIsHovered] = useState(false);
  const [selectedPosition, setSelectedPosition] = useState<any>(null);
  const [isDepositModalOpen, setIsDepositModalOpen] = useState(false);
  const [timeRange, setTimeRange] = useState('1M');
  const [portfolioData, setPortfolioData] = useState<any[]>([]);
  const [positions, setPositions] = useState<any[]>([]);
  const [depositAmount, setDepositAmount] = useState('');

  const [isDepositLoading, setIsDepositLoading] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);

  // Initial load
  useEffect(() => {
    setPositions([
      {
        symbol: 'AAPL',
        name: 'Apple Inc.',
        shares: 10,
        currentPrice: 185.00,
        totalValue: 1850.00,
        change: 1.2,
        changeType: 'up' as const,
        sparkline: generateSparklineData('up'),
      },
      {
        symbol: 'MSFT',
        name: 'Microsoft Corp',
        shares: 8,
        currentPrice: 415.20,
        totalValue: 3321.60,
        change: 2.5,
        changeType: 'up' as const,
        sparkline: generateSparklineData('up'),
      },
      {
        symbol: 'NVDA',
        name: 'NVIDIA Corp',
        shares: 5,
        currentPrice: 485.50,
        totalValue: 2427.50,
        change: -0.8,
        changeType: 'down' as const,
        sparkline: generateSparklineData('down'),
      },
      {
        symbol: 'GOOGL',
        name: 'Alphabet Inc',
        shares: 12,
        currentPrice: 175.30,
        totalValue: 2103.60,
        change: 0.5,
        changeType: 'up' as const,
        sparkline: generateSparklineData('up'),
      },
      {
        symbol: 'TSLA',
        name: 'Tesla Inc',
        shares: 15,
        currentPrice: 245.80,
        totalValue: 3687.00,
        change: -1.5,
        changeType: 'down' as const,
        sparkline: generateSparklineData('down'),
      },
    ]);
  }, []);

  // Update chart when timeRange changes
  useEffect(() => {
    setPortfolioData(generatePortfolioData(timeRange));
  }, [timeRange]);

  const handleDeposit = (e: React.FormEvent) => {
    e.preventDefault();
    setIsDepositLoading(true);
    
    // Simulate API call
    setTimeout(() => {
      setIsDepositLoading(false);
      setShowSuccess(true);
      setTimeout(() => {
        setShowSuccess(false);
        setDepositAmount('');
        setIsDepositModalOpen(false);
        setModalOpen(false);
      }, 1500);
    }, 1500);
  };

  const getAiInsight = (symbol: string) => {
    const insights: Record<string, string> = {
      AAPL: "Apple shows strong resilience with recent services growth. AI integration in upcoming iOS versions is a key catalyst. Analyst sentiment is bullish with a price target of $210.",
      MSFT: "Microsoft's leadership in AI via OpenAI partnership continues to drive cloud growth. Azure revenue is accelerating. Solid long-term hold with potential for dividend increases.",
      NVDA: "NVIDIA remains the dominant player in AI hardware. Demand for H100 chips is outstripping supply. Valuation is high but supported by hyper-growth earnings.",
      GOOGL: "Alphabet is catching up in the AI race with Gemini. Search revenue remains robust. Attractive valuation relative to peers. Watch for regulatory headwinds.",
      TSLA: "Tesla faces near-term margin pressure due to price cuts but maintains EV market leadership. FSD progress is the main wild card for future valuation."
    };
    return insights[symbol] || "AI analysis suggests neutral sentiment based on recent technical indicators and market volatility.";
  };

  const totalPortfolioValue = positions.reduce((sum: number, pos: any) => sum + pos.totalValue, 0);
  const totalChange = ((totalPortfolioValue - 13000) / 13000) * 100;

  if (positions.length === 0) {
    return (
      <div className="bg-white dark:bg-zinc-900 rounded-xl border p-6 h-full flex flex-col items-center justify-center">
        <div className="size-8 border-4 border-blue-600 border-t-transparent rounded-full animate-spin"></div>
      </div>
    );
  }

  return (
    <>
    <div className="bg-white dark:bg-zinc-900 rounded-xl border p-6 h-full flex flex-col">
      <div className="mb-6 flex justify-between items-start">
        <div>
          <h2 className="text-2xl font-bold mb-2">Portfolio Performance</h2>
          <div className="flex items-baseline gap-3">
            <span className="text-3xl font-bold">${totalPortfolioValue.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
            <span className={`text-lg font-semibold flex items-center gap-1 ${totalChange >= 0 ? 'text-green-600' : 'text-red-600'}`}>
              {totalChange >= 0 ? <TrendingUp className="size-4" /> : <TrendingDown className="size-4" />}
              {totalChange >= 0 ? '+' : ''}{totalChange.toFixed(2)}%
            </span>
          </div>
          
          <div className="flex gap-2 mt-4">
            {['1D', '1W', '1M', '3M', 'YTD', '1Y', 'ALL'].map((range) => (
              <button
                key={range}
                onClick={() => setTimeRange(range)}
                className={`px-3 py-1 rounded-lg text-xs font-medium transition-all ${
                  timeRange === range
                    ? 'bg-blue-600 text-white'
                    : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200 dark:hover:bg-zinc-700'
                }`}
              >
                {range}
              </button>
            ))}
          </div>
        </div>
        <button 
          onClick={() => {
            setIsDepositModalOpen(true);
            setModalOpen(true);
          }}
          className="flex items-center gap-2 px-4 py-2 bg-zinc-900 dark:bg-white text-white dark:text-zinc-900 rounded-lg font-medium text-sm hover:opacity-90 transition-opacity"
        >
          <Plus className="size-4" />
          Deposit
        </button>
      </div>

      {/* Portfolio Chart - Green default, Blue on hover (Apple Stocks style) */}
      <div 
        className="h-64 mb-6 bg-gradient-to-br from-green-50 to-emerald-50 dark:from-zinc-800 dark:to-zinc-900 rounded-xl p-4 border transition-all duration-300 relative group"
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
      >
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={portfolioData}>
            <defs>
              <linearGradient id="colorValueGreen" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor="#10b981" stopOpacity={0.3}/>
                <stop offset="95%" stopColor="#10b981" stopOpacity={0}/>
              </linearGradient>
              <linearGradient id="colorValueBlue" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.3}/>
                <stop offset="95%" stopColor="#3b82f6" stopOpacity={0}/>
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" opacity={0.3} />
            <XAxis 
              dataKey="date" 
              tick={{ fontSize: 12 }}
              stroke="#6b7280"
            />
            <YAxis 
              tick={{ fontSize: 12 }}
              stroke="#6b7280"
              tickFormatter={(value) => `$${(value / 1000).toFixed(0)}k`}
            />
            <Tooltip content={<CustomTooltip />} cursor={{ stroke: '#6b7280', strokeWidth: 1, strokeDasharray: '4 4' }} />
            <Area 
              type="monotone" 
              dataKey="value" 
              stroke={isHovered ? "#3b82f6" : "#10b981"}
              strokeWidth={2}
              fillOpacity={1}
              fill={isHovered ? "url(#colorValueBlue)" : "url(#colorValueGreen)"}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>

      {/* Current Positions */}
      <div className="flex-1 overflow-y-auto">
        <h3 className="font-semibold mb-4 text-lg">Current Positions</h3>
        <div className="space-y-3">
          {positions.map((position) => (
            <div
              key={position.symbol}
              className="flex items-center justify-between p-4 bg-zinc-50 dark:bg-zinc-800 rounded-xl hover:bg-zinc-100 dark:hover:bg-zinc-700 hover:shadow-md hover:scale-[1.01] transition-all cursor-pointer border border-zinc-200 dark:border-zinc-700"
              onClick={() => {
                setSelectedPosition(position);
                setModalOpen(true);
              }}
            >
              <div className="flex items-center gap-4 flex-1">
                {/* Sparkline Chart with Background */}
                <div className="w-20 h-12 relative min-w-[80px] min-h-[48px]">
                  <ResponsiveContainer width="100%" height="100%">
                    <AreaChart data={position.sparkline} width={80} height={48}>
                      <defs>
                        <linearGradient id={`sparkGradient-${position.symbol}`} x1="0" y1="0" x2="0" y2="1">
                          <stop offset="5%" stopColor={position.changeType === 'up' ? '#10b981' : '#ef4444'} stopOpacity={0.3}/>
                          <stop offset="95%" stopColor={position.changeType === 'up' ? '#10b981' : '#ef4444'} stopOpacity={0}/>
                        </linearGradient>
                      </defs>
                      <Area
                        type="monotone"
                        dataKey="value"
                        stroke={position.changeType === 'up' ? '#10b981' : '#ef4444'}
                        fill={`url(#sparkGradient-${position.symbol})`}
                        strokeWidth={2}
                      />
                      <Tooltip content={<CustomTooltip />} cursor={false} />
                    </AreaChart>
                  </ResponsiveContainer>
                </div>

                {/* Position Info */}
                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-1">
                    <p className="font-bold text-lg">{position.symbol}</p>
                    <span className={`text-xs px-2 py-0.5 rounded-full ${
                      position.changeType === 'up' 
                        ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400' 
                        : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
                    }`}>
                      {position.change >= 0 ? '+' : ''}{position.change}%
                    </span>
                  </div>
                  <p className="text-sm text-zinc-500">{position.name}</p>
                  <p className="text-xs text-zinc-400 mt-0.5">{position.shares} shares @ ${position.currentPrice.toFixed(2)}</p>
                </div>
              </div>

              {/* Value */}
              <div className="text-right flex items-center gap-4">
                <div>
                  <p className="font-bold text-lg">${position.totalValue.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                  <p className={`text-sm ${position.changeType === 'up' ? 'text-green-600' : 'text-red-600'}`}>
                    {position.change >= 0 ? '+' : ''}${((position.totalValue * position.change) / 100).toFixed(2)}
                  </p>
                </div>
                <ChevronRight className="size-5 text-blue-500" />
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>

    {/* Deposit Modal */}
    {isDepositModalOpen && (
      <div 
        className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-in fade-in duration-200"
        onClick={() => {
          setIsDepositModalOpen(false);
          setModalOpen(false);
        }}
      >
        <div 
          className="bg-white dark:bg-zinc-900 rounded-2xl shadow-2xl w-full max-w-sm p-6 animate-in zoom-in-95 duration-200"
          onClick={(e) => e.stopPropagation()}
        >
          <div className="flex justify-between items-center mb-6">
            <h2 className="text-xl font-bold flex items-center gap-2">
              <Wallet className="size-5 text-blue-600" />
              Deposit Funds
            </h2>
            <button 
              onClick={() => {
                setIsDepositModalOpen(false);
                setModalOpen(false);
              }}
              className="p-2 hover:bg-zinc-100 dark:hover:bg-zinc-800 rounded-full transition-colors"
            >
              <X className="size-5" />
            </button>
          </div>
          <form onSubmit={handleDeposit}>
            {showSuccess ? (
              <div className="flex flex-col items-center justify-center py-8 text-green-600 animate-in fade-in zoom-in duration-300">
                <CheckCircle2 className="size-16 mb-4" />
                <p className="text-lg font-bold">Deposit Successful!</p>
                <p className="text-sm text-zinc-500">Your funds have been added.</p>
              </div>
            ) : (
              <>
                <div className="mb-6">
                  <label className="block text-sm font-medium text-zinc-700 dark:text-zinc-300 mb-2">Amount</label>
                  <div className="relative">
                    <span className="absolute left-4 top-1/2 -translate-y-1/2 text-zinc-500 font-bold">$</span>
                    <input 
                      type="number" 
                      value={depositAmount}
                      onChange={(e) => setDepositAmount(e.target.value)}
                      placeholder="0.00"
                      className="w-full pl-8 pr-4 py-3 bg-zinc-50 dark:bg-zinc-800 border-2 border-zinc-200 dark:border-zinc-700 rounded-xl focus:outline-none focus:border-blue-500 font-mono text-lg"
                      autoFocus
                      disabled={isDepositLoading}
                    />
                  </div>
                </div>
                <button 
                  type="submit" 
                  className="w-full py-3 bg-blue-600 hover:bg-blue-700 text-white rounded-xl font-bold transition-colors disabled:opacity-50 flex items-center justify-center gap-2"
                  disabled={!depositAmount || isDepositLoading}
                >
                  {isDepositLoading ? (
                    <>
                      <Loader2 className="size-5 animate-spin" />
                      Processing...
                    </>
                  ) : (
                    'Confirm Deposit'
                  )}
                </button>
              </>
            )}
          </form>
        </div>
      </div>
    )}

    {/* Position Detail Modal */}
    {selectedPosition && (
      <div 
        className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-in fade-in duration-200"
        onClick={() => {
          setSelectedPosition(null);
          setModalOpen(false);
        }}
      >
        <div 
          className="bg-white dark:bg-zinc-900 rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto animate-in zoom-in-95 duration-200"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Modal Header */}
          <div className="sticky top-0 bg-white dark:bg-zinc-900 border-b p-4 flex items-center justify-between rounded-t-2xl z-10">
            <div className="flex items-center gap-3">
              <div className="size-12 bg-zinc-100 dark:bg-zinc-800 rounded-xl flex items-center justify-center text-xl font-bold">
                {selectedPosition.symbol}
              </div>
              <div>
                <h2 className="font-bold text-xl">{selectedPosition.name}</h2>
                <div className="flex items-center gap-2 text-sm text-zinc-500">
                  <span>{selectedPosition.shares} shares</span>
                  <span>•</span>
                  <span>Total: ${selectedPosition.totalValue.toLocaleString()}</span>
                </div>
              </div>
            </div>
            <button 
              onClick={() => {
                setSelectedPosition(null);
                setModalOpen(false);
              }}
              className="p-2 hover:bg-zinc-100 dark:hover:bg-zinc-800 rounded-full transition-colors"
            >
              <X className="size-5" />
            </button>
          </div>

          <div className="p-6 space-y-6">
            {/* Price & Change */}
            <div className="flex items-end justify-between">
              <div>
                <p className="text-sm text-zinc-500 mb-1">Current Price</p>
                <p className="text-3xl font-bold">${selectedPosition.currentPrice.toFixed(2)}</p>
              </div>
              <div className={`text-right ${selectedPosition.changeType === 'up' ? 'text-green-600' : 'text-red-600'}`}>
                <div className="flex items-center gap-1 font-bold text-xl">
                  {selectedPosition.changeType === 'up' ? <TrendingUp className="size-5" /> : <TrendingDown className="size-5" />}
                  {selectedPosition.change >= 0 ? '+' : ''}{selectedPosition.change}%
                </div>
                <p className="text-sm opacity-80">Today's Change</p>
              </div>
            </div>

            {/* Detailed Chart */}
            <div className="h-64 bg-zinc-50 dark:bg-zinc-800/50 rounded-xl p-4 border border-zinc-100 dark:border-zinc-800">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={selectedPosition.sparkline}>
                  <defs>
                    <linearGradient id="modalGradient" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor={selectedPosition.changeType === 'up' ? '#10b981' : '#ef4444'} stopOpacity={0.3}/>
                      <stop offset="95%" stopColor={selectedPosition.changeType === 'up' ? '#10b981' : '#ef4444'} stopOpacity={0}/>
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e5e7eb" opacity={0.5} />
                  <Tooltip content={<CustomTooltip />} cursor={{ stroke: '#6b7280', strokeWidth: 1, strokeDasharray: '4 4' }} />
                  <Area 
                    type="monotone" 
                    dataKey="value" 
                    stroke={selectedPosition.changeType === 'up' ? '#10b981' : '#ef4444'}
                    fill="url(#modalGradient)" 
                    strokeWidth={3}
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>

            {/* AI Insights */}
            <div className="bg-blue-50 dark:bg-blue-900/10 rounded-xl p-5 border border-blue-100 dark:border-blue-900/30">
              <div className="flex items-center gap-2 mb-3 text-blue-700 dark:text-blue-400">
                <Brain className="size-5" />
                <h3 className="font-bold">AI Analyst Insight</h3>
              </div>
              <p className="text-zinc-700 dark:text-zinc-300 leading-relaxed">
                {getAiInsight(selectedPosition.symbol)}
              </p>
            </div>

            {/* Stats Grid */}
            <div className="grid grid-cols-2 gap-4">
              <div className="p-4 bg-zinc-50 dark:bg-zinc-800 rounded-xl border border-zinc-100 dark:border-zinc-700">
                <div className="flex items-center gap-2 mb-2 text-zinc-500">
                  <Activity className="size-4" />
                  <span className="text-xs font-medium uppercase tracking-wider">Volatility</span>
                </div>
                <p className="font-semibold">Medium-High</p>
              </div>
              <div className="p-4 bg-zinc-50 dark:bg-zinc-800 rounded-xl border border-zinc-100 dark:border-zinc-700">
                <div className="flex items-center gap-2 mb-2 text-zinc-500">
                  <Newspaper className="size-4" />
                  <span className="text-xs font-medium uppercase tracking-wider">Sentiment</span>
                </div>
                <p className="font-semibold text-green-600">Bullish</p>
              </div>
            </div>

            {/* Action Buttons */}
            <div className="grid grid-cols-2 gap-3 pt-2">
              <button className="py-3 px-4 bg-zinc-100 hover:bg-zinc-200 dark:bg-zinc-800 dark:hover:bg-zinc-700 text-zinc-900 dark:text-white rounded-xl font-bold transition-colors">
                Sell Position
              </button>
              <button className="py-3 px-4 bg-blue-600 hover:bg-blue-700 text-white rounded-xl font-bold transition-colors shadow-lg shadow-blue-500/20">
                Buy More
              </button>
            </div>
          </div>
        </div>
      </div>
    )}
    </>
  );
}
