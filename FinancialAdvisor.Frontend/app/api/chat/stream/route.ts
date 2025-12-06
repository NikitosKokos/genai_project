import { NextRequest } from 'next/server';

export const runtime = 'nodejs'; // Ensure we're using Node.js runtime for streaming

export async function POST(request: NextRequest) {
   try {
      // Get the request body
      let body;
      try {
         body = await request.json();
      } catch (error) {
         console.error('[API Route] Failed to parse request body:', error);
         const errorStream = new ReadableStream({
            start(controller) {
               const errorBytes = new TextEncoder().encode('<status>Error: Invalid request body</status>');
               controller.enqueue(errorBytes);
               controller.close();
            },
         });
         return new Response(errorStream, {
            status: 400,
            headers: {
               'Content-Type': 'text/plain',
               'Cache-Control': 'no-cache',
            },
         });
      }

      // Forward the request to the backend
      // Use environment variable or default to localhost:5002
      const backendUrl = process.env.BACKEND_URL || 'http://localhost:5002';
      
      let backendResponse: Response;
      try {
         backendResponse = await fetch(`${backendUrl}/api/chat/stream`, {
            method: 'POST',
            headers: {
               'Content-Type': 'application/json',
            },
            body: JSON.stringify(body),
            // Critical: Don't buffer the response - stream it immediately
            cache: 'no-store',
         });
      } catch (error: any) {
         console.error('[API Route] Failed to connect to backend:', error);
         const errorStream = new ReadableStream({
            start(controller) {
               const errorMsg = error?.message || 'Failed to connect to backend. Is the backend running?';
               const errorBytes = new TextEncoder().encode(`<status>Error: ${errorMsg}</status>`);
               controller.enqueue(errorBytes);
               controller.close();
            },
         });
         return new Response(errorStream, {
            status: 503,
            headers: {
               'Content-Type': 'text/plain',
               'Cache-Control': 'no-cache',
            },
         });
      }

      if (!backendResponse.ok) {
         // Try to get error message from backend
         let errorMessage = 'Backend request failed';
         try {
            const errorText = await backendResponse.text();
            errorMessage = errorText || errorMessage;
         } catch (e) {
            // If we can't read the error, use default message
         }
         
         // Return error in stream-compatible format
         const errorStream = new ReadableStream({
            start(controller) {
               const errorBytes = new TextEncoder().encode(`<status>Error: ${errorMessage}</status>`);
               controller.enqueue(errorBytes);
               controller.close();
            },
         });
         
         return new Response(errorStream, {
            status: backendResponse.status,
            headers: {
               'Content-Type': 'text/plain',
               'Cache-Control': 'no-cache',
            },
         });
      }

      // Create a readable stream that forwards chunks from backend
      const stream = new ReadableStream({
         async start(controller) {
            const reader = backendResponse.body?.getReader();
            const decoder = new TextDecoder();

            if (!reader) {
               const errorMsg = new TextEncoder().encode('<status>Backend response has no body</status>');
               controller.enqueue(errorMsg);
               controller.close();
               return;
            }

            try {
               while (true) {
                  const { done, value } = await reader.read();
                  
                  if (done) {
                     controller.close();
                     break;
                  }

                  // Forward the chunk immediately without buffering
                  controller.enqueue(value);
               }
            } catch (error: any) {
               console.error('[API Route] Stream error:', error);
               
               // Try to send error message to client before closing
               try {
                  const errorMsg = error?.message || 'Stream connection error';
                  const errorBytes = new TextEncoder().encode(`<status>Error: ${errorMsg}</status>`);
                  controller.enqueue(errorBytes);
               } catch (e) {
                  // If we can't send error, just log it
                  console.error('[API Route] Failed to send error message:', e);
               }
               
               // Close the stream gracefully
               try {
                  controller.close();
               } catch (e) {
                  // Stream might already be closed
               }
            } finally {
               // Ensure reader is released
               try {
                  reader.releaseLock();
               } catch (e) {
                  // Reader might already be released
               }
            }
         },
         cancel() {
            // Handle client cancellation
            console.log('[API Route] Stream cancelled by client');
         },
      });

      // Return the stream with proper headers
      return new Response(stream, {
         headers: {
            'Content-Type': 'text/plain',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'X-Accel-Buffering': 'no', // Disable nginx buffering if present
         },
      });
   } catch (error) {
      console.error('[API Route] Error:', error);
      return new Response(
         JSON.stringify({ error: 'Internal server error' }),
         {
            status: 500,
            headers: { 'Content-Type': 'application/json' },
         }
      );
   }
}

