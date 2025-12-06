import { NextRequest } from 'next/server';

export const runtime = 'nodejs'; // Ensure we're using Node.js runtime for streaming

export async function POST(request: NextRequest) {
   try {
      // Get the request body
      const body = await request.json();

      // Forward the request to the backend
      // Use environment variable or default to localhost:5000
      const backendUrl = process.env.BACKEND_URL || 'http://localhost:5002';
      const backendResponse = await fetch(`${backendUrl}/api/chat/stream`, {
         method: 'POST',
         headers: {
            'Content-Type': 'application/json',
         },
         body: JSON.stringify(body),
         // Critical: Don't buffer the response - stream it immediately
         cache: 'no-store',
      });

      if (!backendResponse.ok) {
         return new Response(
            JSON.stringify({ error: 'Backend request failed' }),
            {
               status: backendResponse.status,
               headers: { 'Content-Type': 'application/json' },
            }
         );
      }

      // Create a readable stream that forwards chunks from backend
      const stream = new ReadableStream({
         async start(controller) {
            const reader = backendResponse.body?.getReader();
            const decoder = new TextDecoder();

            if (!reader) {
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
            } catch (error) {
               console.error('[API Route] Stream error:', error);
               controller.error(error);
            }
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

