// Simple API client wrapper
// We use the Next.js proxy at /api to avoid CORS issues
// Use relative path to leverage Next.js rewrites (see next.config.ts)
const API_BASE_URL = '/api';

export interface ApiResponse<T> {
   data?: T;
   error?: string;
}

async function fetchJson<T>(endpoint: string, options?: RequestInit): Promise<T> {
   const url = `${API_BASE_URL}${endpoint}`;
   const response = await fetch(url, {
      ...options,
      headers: {
         'Content-Type': 'application/json',
         ...options?.headers,
      },
   });

   if (!response.ok) {
      throw new Error(`API error: ${response.statusText}`);
   }

   // Handle empty responses
   const text = await response.text();
   return text ? JSON.parse(text) : ({} as T);
}

export const api = {
   // Financial Advisor / RAG
   chat: {
      sendMessage: async (message: string) => {
         return fetchJson('/Chat/stream', {
            method: 'POST',
            body: JSON.stringify({ message: message, sessionId: 'test-user-123' }), // Hardcoded userId for demo
         });
      },
      streamMessage: async (
         message: string,
         sessionId: string,
         enableReasoning: boolean = false,
         documentCount: number = 3,
         onChunk: (chunk: string) => void,
         onStatus?: (status: string) => void,
         onThinking?: (thinking: string) => void,
      ) => {
         const streamStartTime = performance.now();
         console.log('[api.ts] ===== STREAM START =====');
         console.log('[api.ts] Request:', {
            message: message.substring(0, 50),
            sessionId,
            enableReasoning,
            documentCount,
         });

         // Use the Next.js API route which properly handles streaming
         const response = await fetch(`${API_BASE_URL}/chat/stream`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message, sessionId, enableReasoning, documentCount }),
            // Ensure fetch doesn't buffer
            cache: 'no-store',
         });

         if (!response.body) {
            console.log('[api.ts] ERROR: No response body');
            return;
         }

         const reader = response.body.getReader();
         const decoder = new TextDecoder();
         let buffer = '';
         let chunkCount = 0;
         let totalBytes = 0;

         while (true) {
            const readStartTime = performance.now();
            const { done, value } = await reader.read();
            const readDuration = performance.now() - readStartTime;

            if (done) {
               const totalDuration = performance.now() - streamStartTime;
               console.log('[api.ts] ===== STREAM END =====');
               console.log('[api.ts] Total chunks:', chunkCount);
               console.log('[api.ts] Total bytes:', totalBytes);
               console.log('[api.ts] Total duration:', totalDuration.toFixed(2), 'ms');
               console.log('[api.ts] Remaining buffer length:', buffer.length);

               // Flush any remaining buffer content
               if (buffer.length > 0) {
                  console.log('[api.ts] Flushing remaining buffer:', buffer.substring(0, 200));
                  // Try to parse any remaining content
                  if (buffer.includes('<response>')) {
                     // Try to extract any remaining response content
                     const cdataStart = buffer.indexOf('<![CDATA[');
                     if (cdataStart !== -1) {
                        const contentStart = cdataStart + '<![CDATA['.length;
                        const content = buffer.substring(contentStart);
                        // Remove any trailing tags
                        const endTag = content.indexOf(']]>');
                        if (endTag !== -1) {
                           console.log('[api.ts] Flushing final chunk (length):', endTag);
                           onChunk(content.substring(0, endTag));
                        } else {
                           console.log(
                              '[api.ts] Flushing final chunk (no end tag, length):',
                              content.length,
                           );
                           onChunk(content);
                        }
                     }
                  } else {
                     // No tags, emit as-is
                     console.log('[api.ts] Flushing buffer as-is (length):', buffer.length);
                     onChunk(buffer);
                  }
                  buffer = '';
               }
               break;
            }

            chunkCount++;
            totalBytes += value.length;
            const chunk = decoder.decode(value, { stream: true });
            const chunkTime = performance.now() - streamStartTime;

            console.log(
               `[api.ts] Chunk #${chunkCount} at ${chunkTime.toFixed(
                  2,
               )}ms (read took ${readDuration.toFixed(2)}ms, size: ${value.length} bytes)`,
            );
            console.log(
               '[api.ts] Raw chunk (first 200 chars):',
               JSON.stringify(chunk.substring(0, 200)),
            );
            console.log('[api.ts] Buffer before adding (length):', buffer.length);
            buffer += chunk;
            console.log('[api.ts] Buffer after adding (length):', buffer.length);

            // Robust XML tag parsing - handle <status>, <thinking>, and <response> tags
            while (buffer.length > 0) {
               // Look for XML tags: <status>, <thinking>, <response>
               const statusTagStart = buffer.indexOf('<status>');
               const thinkingTagStart = buffer.indexOf('<thinking>');
               const responseTagStart = buffer.indexOf('<response>');

               // Find the earliest tag
               let earliestTag = -1;
               let tagType: 'status' | 'thinking' | 'response' | null = null;
               let tagStart = -1;

               if (statusTagStart !== -1 && (earliestTag === -1 || statusTagStart < earliestTag)) {
                  earliestTag = statusTagStart;
                  tagType = 'status';
                  tagStart = statusTagStart;
               }
               if (
                  thinkingTagStart !== -1 &&
                  (earliestTag === -1 || thinkingTagStart < earliestTag)
               ) {
                  earliestTag = thinkingTagStart;
                  tagType = 'thinking';
                  tagStart = thinkingTagStart;
               }
               if (
                  responseTagStart !== -1 &&
                  (earliestTag === -1 || responseTagStart < earliestTag)
               ) {
                  earliestTag = responseTagStart;
                  tagType = 'response';
                  tagStart = responseTagStart;
               }

               if (earliestTag > 0) {
                  // Content before the tag - emit as regular content
                  const content = buffer.substring(0, earliestTag);
                  onChunk(content);
                  buffer = buffer.substring(earliestTag);
                  continue;
               }

               if (earliestTag === 0) {
                  // Found a tag at the start
                  const openTag = `<${tagType}>`;
                  const closeTag = `</${tagType}>`;
                  const openTagEnd = tagStart + openTag.length;

                  // Check if this is a CDATA section (for response tags)
                  const cdataMarker = '<![CDATA[';
                  const cdataEndMarker = ']]>';
                  const hasCData =
                     tagType === 'response' &&
                     buffer.indexOf(cdataMarker, openTagEnd) === openTagEnd;

                  let content = '';
                  let contentEnd = -1;

                  if (hasCData) {
                     // Handle CDATA section
                     const cdataStart = openTagEnd + cdataMarker.length;
                     const cdataEnd = buffer.indexOf(cdataEndMarker, cdataStart);

                     if (cdataEnd !== -1) {
                        // Found end of CDATA - extract content
                        content = buffer.substring(cdataStart, cdataEnd);
                        // Restore any ]]> that were escaped
                        content = content.replace(/\]\]\]\]><!\[CDATA\[>/g, ']]>');
                        contentEnd = cdataEnd + cdataEndMarker.length;

                        // Find the closing </response> tag
                        const responseCloseTag = buffer.indexOf('</response>', contentEnd);
                        if (responseCloseTag !== -1) {
                           contentEnd = responseCloseTag + '</response>'.length;
                        } else {
                           // CDATA complete but </response> not found yet - emit content anyway
                           // and keep the closing part in buffer
                           contentEnd = cdataEnd + cdataEndMarker.length;
                        }
                     } else {
                        // Incomplete CDATA - emit partial content for real-time streaming
                        const partialContent = buffer.substring(cdataStart);
                        if (partialContent.length > 0) {
                           const emitTime = performance.now() - streamStartTime;
                           console.log(
                              `[api.ts] [${emitTime.toFixed(
                                 2,
                              )}ms] Emitting PARTIAL CDATA (length: ${partialContent.length}):`,
                              partialContent.substring(0, 100),
                           );
                           onChunk(partialContent);
                           // Keep opening tag structure in buffer
                           buffer = buffer.substring(0, openTagEnd + cdataMarker.length);
                        }
                        break; // Wait for more data
                     }
                  } else {
                     // Regular content (no CDATA)
                     const closeTagIdx = buffer.indexOf(closeTag, openTagEnd);

                     if (closeTagIdx !== -1) {
                        content = buffer.substring(openTagEnd, closeTagIdx);
                        // Unescape XML entities
                        content = content
                           .replace(/&amp;/g, '&')
                           .replace(/&lt;/g, '<')
                           .replace(/&gt;/g, '>');
                        contentEnd = closeTagIdx + closeTag.length;
                     } else {
                        // Incomplete tag - wait for more data
                        if (buffer.length > 10000) {
                           // Safety: emit as content if buffer gets too large
                           onChunk(buffer);
                           buffer = '';
                        } else {
                           break;
                        }
                        continue;
                     }
                  }

                  // Process the extracted content
                  if (contentEnd !== -1) {
                     const emitTime = performance.now() - streamStartTime;
                     if (tagType === 'status' && onStatus) {
                        console.log(
                           `[api.ts] [${emitTime.toFixed(2)}ms] Emitting STATUS:`,
                           content,
                        );
                        onStatus(content);
                     } else if (tagType === 'thinking' && onThinking) {
                        console.log(
                           `[api.ts] [${emitTime.toFixed(2)}ms] Emitting THINKING (length):`,
                           content.length,
                        );
                        onThinking(content);
                     } else if (tagType === 'response') {
                        console.log(
                           `[api.ts] [${emitTime.toFixed(2)}ms] Emitting RESPONSE chunk (length: ${
                              content.length
                           }):`,
                           content.substring(0, 100),
                        );
                        onChunk(content);
                     }

                     buffer = buffer.substring(contentEnd);
                     console.log('[api.ts] Buffer after processing (length):', buffer.length);
                  }
               } else {
                  // No tags found - emit as regular content
                  onChunk(buffer);
                  buffer = '';
               }
            }
         }
      },
   },

   // Dashboard Data
   dashboard: {
      getNews: async () => fetchJson('/dashboard/news'),
      getPortfolio: async () => fetchJson('/dashboard/portfolio'),
      getAssets: async () => fetchJson('/dashboard/assets'),
   },

   // Trading
   trading: {
      placeOrder: async (symbol: string, side: 'buy' | 'sell', quantity: number) => {
         return fetchJson('/trading/orders', {
            method: 'POST',
            body: JSON.stringify({ symbol, side, quantity }),
         });
      },
   },
};
