using System;
using System.Collections.Generic;

namespace FinancialAdvisor.Application.Models
{
    public class ChatResponse
    {
        public string Advice { get; set; }
        public List<Trade> ExecutedTrades { get; set; } = new List<Trade>();
        public List<DocumentSource> Sources { get; set; } = new List<DocumentSource>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class DocumentSource
    {
        public string Title { get; set; }
        public string Source { get; set; }
        public string Category { get; set; }
    }
}

