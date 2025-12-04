'use client';

import React, { useState } from 'react';
import { TrendingUp, TrendingDown, Smartphone, Monitor, Cpu, Car, X, Search, Brain, Activity, Newspaper, ChevronRight } from 'lucide-react';
import { useUI } from '../context/UIContext';

interface Asset {
  symbol: string;
  name: string;
  sector: string;
  price: number;
  change: number;
  changePercent: number;
}

const assets: Asset[] = [
  { symbol: 'AAPL', name: 'Apple Inc.', sector: 'Technology', price: 185.00, change: 2.20, changePercent: 1.2 },
  { symbol: 'MSFT', name: 'Microsoft Corp', sector: 'Technology', price: 415.20, change: 10.15, changePercent: 2.5 },
  { symbol: 'GOOGL', name: 'Alphabet Inc', sector: 'Technology', price: 175.30, change: 0.88, changePercent: 0.5 },
  { symbol: 'NVDA', name: 'NVIDIA Corp', sector: 'Technology', price: 485.50, change: -3.88, changePercent: -0.8 },
  { symbol: 'TSLA', name: 'Tesla Inc', sector: 'Consumer Cyclical', price: 245.80, change: -3.69, changePercent: -1.5 },
];

export function AssetsPanel() {
  const { setModalOpen } = useUI();
  const [selectedAsset, setSelectedAsset] = useState<Asset | null>(null); // For trading
  const [viewAsset, setViewAsset] = useState<Asset | null>(null); // For details
  const [quantity, setQuantity] = useState(1);
  const [orderSide, setOrderSide] = useState<'buy' | 'sell'>('buy');
  const [searchQuery, setSearchQuery] = useState('');
  const [isSearchOpen, setIsSearchOpen] = useState(false);

  const filteredAssets = assets.filter(asset => 
    asset.symbol.toLowerCase().includes(searchQuery.toLowerCase()) || 
    asset.name.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const handleTrade = (asset: Asset, e: React.MouseEvent) => {
    e.stopPropagation();
    setSelectedAsset(asset);
    setModalOpen(true);
    setQuantity(1);
    setOrderSide('buy');
  };

  const handleAssetClick = (asset: Asset) => {
    setViewAsset(asset);
    setModalOpen(true);
  };

  const handleCloseModal = () => {
    setSelectedAsset(null);
    setViewAsset(null);
    setModalOpen(false);
  };

  const handlePlaceOrder = () => {
    // Mock order placement
    alert(`Order placed: ${orderSide.toUpperCase()} ${quantity} shares of ${selectedAsset?.symbol}`);
    handleCloseModal();
  };

  const getAssetLogo = (symbol: string) => {
    switch (symbol) {
      case 'AAPL':
        return (
          <div className="size-10 bg-gradient-to-br from-zinc-700 to-black rounded-xl flex items-center justify-center text-white shadow-lg shadow-zinc-500/20">
            <Smartphone className="size-5" />
          </div>
        );
      case 'MSFT':
        return (
          <div className="size-10 bg-gradient-to-br from-blue-500 to-cyan-500 rounded-xl flex items-center justify-center text-white shadow-lg shadow-blue-500/20">
            <Monitor className="size-5" />
          </div>
        );
      case 'GOOGL':
        return (
          <div className="size-10 bg-gradient-to-br from-red-500 via-yellow-500 to-green-500 rounded-xl flex items-center justify-center text-white shadow-lg shadow-orange-500/20">
            <span className="font-bold text-lg">G</span>
          </div>
        );
      case 'NVDA':
        return (
          <div className="size-10 bg-gradient-to-br from-green-500 to-emerald-700 rounded-xl flex items-center justify-center text-white shadow-lg shadow-green-500/20">
            <Cpu className="size-5" />
          </div>
        );
      case 'TSLA':
        return (
          <div className="size-10 bg-gradient-to-br from-red-600 to-red-800 rounded-xl flex items-center justify-center text-white shadow-lg shadow-red-500/20">
            <Car className="size-5" />
          </div>
        );
      default:
        return (
          <div className="size-10 bg-gradient-to-br from-indigo-500 to-purple-500 rounded-xl flex items-center justify-center text-white shadow-lg shadow-indigo-500/20">
            <span className="font-bold text-xs">{symbol.substring(0, 2)}</span>
          </div>
        );
    }
  };

  return (
    <div className="bg-white dark:bg-zinc-900 rounded-xl border p-6 h-full flex flex-col overflow-hidden">
      <div className="flex justify-between items-center mb-4">
        {isSearchOpen ? (
          <div className="flex-1 flex items-center gap-2 animate-in fade-in slide-in-from-right-4 duration-200">
            <Search className="size-4 text-zinc-500" />
            <input 
              type="text"
              placeholder="Search assets..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="flex-1 bg-transparent border-none focus:ring-0 p-0 text-sm font-medium placeholder:text-zinc-400"
              autoFocus
            />
            <button onClick={() => { setIsSearchOpen(false); setSearchQuery(''); }} className="p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800 rounded-full">
              <X className="size-4 text-zinc-500" />
            </button>
          </div>
        ) : (
          <>
            <h2 className="text-lg font-bold">Available Assets</h2>
            <button 
              onClick={() => setIsSearchOpen(true)}
              className="p-2 hover:bg-zinc-100 dark:hover:bg-zinc-800 rounded-full transition-colors"
            >
              <Search className="size-4 text-zinc-500" />
            </button>
          </>
        )}
      </div>
      <div className="flex-1 overflow-y-auto space-y-2 pr-2">
        {filteredAssets.length > 0 ? filteredAssets.map((asset) => {
          const isPositive = asset.changePercent >= 0;
          return (
            <div
              key={asset.symbol}
              className="flex items-center justify-between p-3 border rounded-xl hover:bg-zinc-50 dark:hover:bg-zinc-800 hover:shadow-md hover:scale-[1.01] transition-all cursor-pointer border-zinc-200 dark:border-zinc-700 group"
              onClick={() => handleAssetClick(asset)}
            >
              <div className="flex items-center gap-3 flex-1">
                {getAssetLogo(asset.symbol)}
                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-sm truncate">{asset.name}</p>
                  <p className="text-xs text-zinc-500 truncate">{asset.sector}</p>
                </div>
              </div>
              <div className="text-right flex items-center gap-4">
                <div>
                  <p className="font-semibold text-sm">${asset.price.toFixed(2)}</p>
                  <div className={`flex items-center gap-1 text-xs ${isPositive ? 'text-green-600' : 'text-red-600'}`}>
                    {isPositive ? (
                      <TrendingUp className="size-3" />
                    ) : (
                      <TrendingDown className="size-3" />
                    )}
                    <span>
                      {isPositive ? '+' : ''}{asset.changePercent.toFixed(2)}%
                    </span>
                  </div>
                </div>
                <ChevronRight className="size-5 text-zinc-300 dark:text-zinc-600" />
              </div>
            </div>
          );
        }) : (
          <div className="flex flex-col items-center justify-center h-full text-zinc-500 text-sm">
            <p>No assets found</p>
          </div>
        )}
      </div>

      {/* Asset Details Modal */}
      {viewAsset && (
        <div 
          className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-in fade-in duration-200"
          onClick={handleCloseModal}
        >
          <div 
            className="bg-white dark:bg-zinc-900 rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto animate-in zoom-in-95 duration-200"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Modal Header */}
            <div className="sticky top-0 bg-white dark:bg-zinc-900 border-b p-4 flex items-center justify-between rounded-t-2xl z-10">
              <div className="flex items-center gap-3">
                {getAssetLogo(viewAsset.symbol)}
                <div>
                  <h2 className="font-bold text-xl">{viewAsset.name}</h2>
                  <p className="text-sm text-zinc-500">{viewAsset.sector}</p>
                </div>
              </div>
              <button 
                onClick={handleCloseModal}
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
                  <p className="text-3xl font-bold">${viewAsset.price.toFixed(2)}</p>
                </div>
                <div className={`text-right ${viewAsset.changePercent >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                  <div className="flex items-center gap-1 font-bold text-xl">
                    {viewAsset.changePercent >= 0 ? <TrendingUp className="size-5" /> : <TrendingDown className="size-5" />}
                    {viewAsset.changePercent >= 0 ? '+' : ''}{viewAsset.changePercent}%
                  </div>
                  <p className="text-sm opacity-80">Today's Change</p>
                </div>
              </div>

              {/* AI Insights */}
              <div className="bg-indigo-50 dark:bg-indigo-900/10 rounded-xl p-5 border border-indigo-100 dark:border-indigo-900/30">
                <div className="flex items-center gap-2 mb-3 text-indigo-700 dark:text-indigo-400">
                  <Brain className="size-5" />
                  <h3 className="font-bold">AI Market Intelligence</h3>
                </div>
                <p className="text-zinc-700 dark:text-zinc-300 leading-relaxed">
                  Based on recent market data, {viewAsset.name} shows strong momentum in the {viewAsset.sector} sector. 
                  Analyst consensus remains positive, driven by solid earnings growth and favorable industry trends. 
                  Volatility indicators suggest moderate risk, suitable for a balanced growth portfolio.
                </p>
              </div>

              {/* Stats Grid */}
              <div className="grid grid-cols-2 gap-4">
                <div className="p-4 bg-zinc-50 dark:bg-zinc-800 rounded-xl border border-zinc-100 dark:border-zinc-700">
                  <div className="flex items-center gap-2 mb-2 text-zinc-500">
                    <Activity className="size-4" />
                    <span className="text-xs font-medium uppercase tracking-wider">Volume</span>
                  </div>
                  <p className="font-semibold">High (24M)</p>
                </div>
                <div className="p-4 bg-zinc-50 dark:bg-zinc-800 rounded-xl border border-zinc-100 dark:border-zinc-700">
                  <div className="flex items-center gap-2 mb-2 text-zinc-500">
                    <Newspaper className="size-4" />
                    <span className="text-xs font-medium uppercase tracking-wider">News Sentiment</span>
                  </div>
                  <p className="font-semibold text-green-600">Positive</p>
                </div>
              </div>

              {/* Action Button */}
              <button 
                onClick={(e) => {
                  handleCloseModal();
                  handleTrade(viewAsset, e);
                }}
                className="w-full py-4 bg-blue-600 hover:bg-blue-700 text-white rounded-xl font-bold text-lg shadow-lg shadow-blue-500/25 transition-all"
              >
                Trade {viewAsset.symbol}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Trade Modal */}
      {selectedAsset && (
        <div 
          className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-in fade-in duration-200"
          onClick={handleCloseModal}
        >
          <div 
            className="bg-white dark:bg-zinc-900 rounded-2xl shadow-2xl w-full max-w-md animate-in zoom-in-95 duration-200 overflow-hidden"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Header */}
            <div className="bg-gradient-to-r from-blue-600 to-indigo-600 p-6 text-white">
              <div className="flex justify-between items-start mb-4">
                <div>
                  <h2 className="text-2xl font-bold">Trade {selectedAsset.symbol}</h2>
                  <p className="text-blue-100">{selectedAsset.name}</p>
                </div>
                <button 
                  onClick={handleCloseModal}
                  className="p-1 hover:bg-white/20 rounded-full transition-colors"
                >
                  <X className="size-6 text-white" />
                </button>
              </div>
              <div className="flex justify-between items-end">
                <div>
                  <p className="text-blue-100 text-sm mb-1">Current Price</p>
                  <p className="text-3xl font-bold">${selectedAsset.price.toFixed(2)}</p>
                </div>
                <div className={`flex items-center gap-1 bg-white/10 px-2 py-1 rounded-lg backdrop-blur-sm ${selectedAsset.changePercent >= 0 ? 'text-green-300' : 'text-red-300'}`}>
                  {selectedAsset.changePercent >= 0 ? <TrendingUp className="size-4" /> : <TrendingDown className="size-4" />}
                  <span className="font-medium">{Math.abs(selectedAsset.changePercent)}%</span>
                </div>
              </div>
            </div>

            {/* Content */}
            <div className="p-6 space-y-6">
              {/* Order Type Tabs */}
              <div className="flex bg-zinc-100 dark:bg-zinc-800 p-1 rounded-xl relative z-20">
                <button 
                  onClick={() => setOrderSide('buy')}
                  className={`flex-1 py-2 text-sm font-medium rounded-lg transition-all ${
                    orderSide === 'buy' 
                      ? 'bg-white dark:bg-zinc-700 shadow-sm text-blue-600 dark:text-blue-400 font-bold' 
                      : 'text-zinc-500 hover:text-zinc-700 dark:text-zinc-400 dark:hover:text-zinc-200'
                  }`}
                >
                  Buy
                </button>
                <button 
                  onClick={() => setOrderSide('sell')}
                  className={`flex-1 py-2 text-sm font-medium rounded-lg transition-all ${
                    orderSide === 'sell' 
                      ? 'bg-white dark:bg-zinc-700 shadow-sm text-red-600 dark:text-red-400 font-bold' 
                      : 'text-zinc-500 hover:text-zinc-700 dark:text-zinc-400 dark:hover:text-zinc-200'
                  }`}
                >
                  Sell
                </button>
              </div>

              {/* Quantity Input */}
              <div className="space-y-2">
                <label className="text-sm font-medium text-zinc-700 dark:text-zinc-300">Quantity</label>
                <div className="flex items-center gap-4">
                  <button 
                    onClick={() => setQuantity(Math.max(1, quantity - 1))}
                    className="size-10 flex items-center justify-center rounded-xl border border-zinc-200 dark:border-zinc-700 hover:bg-zinc-50 dark:hover:bg-zinc-800 text-xl font-medium transition-colors"
                  >
                    -
                  </button>
                  <div className="flex-1 h-10 flex items-center justify-center bg-zinc-50 dark:bg-zinc-800 rounded-xl border border-zinc-200 dark:border-zinc-700 font-mono text-lg font-bold">
                    {quantity}
                  </div>
                  <button 
                    onClick={() => setQuantity(quantity + 1)}
                    className="size-10 flex items-center justify-center rounded-xl border border-zinc-200 dark:border-zinc-700 hover:bg-zinc-50 dark:hover:bg-zinc-800 text-xl font-medium transition-colors"
                  >
                    +
                  </button>
                </div>
              </div>

              {/* Order Summary */}
              <div className="bg-zinc-50 dark:bg-zinc-800/50 p-4 rounded-xl space-y-3 border border-zinc-100 dark:border-zinc-800">
                <div className="flex justify-between text-sm">
                  <span className="text-zinc-500">Order Value</span>
                  <span className="font-medium">${(selectedAsset.price * quantity).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-zinc-500">Commission</span>
                  <span className="font-medium text-green-600">Free</span>
                </div>
                <div className="border-t border-zinc-200 dark:border-zinc-700 pt-3 flex justify-between items-baseline">
                  <span className="font-bold text-zinc-900 dark:text-zinc-100">Estimated Total</span>
                  <span className="text-xl font-bold text-blue-600">${(selectedAsset.price * quantity).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                </div>
              </div>

              <button 
                onClick={handlePlaceOrder}
                className={`w-full py-4 text-white rounded-xl font-bold text-lg shadow-lg transition-all ${
                  orderSide === 'buy'
                    ? 'bg-blue-600 hover:bg-blue-700 shadow-blue-500/25'
                    : 'bg-red-600 hover:bg-red-700 shadow-red-500/25'
                }`}
              >
                Place {orderSide === 'buy' ? 'Buy' : 'Sell'} Order
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
