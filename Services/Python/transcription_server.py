import falcon.asgi
import asyncio
import uvicorn
from transcription import transcribe_audio

class TranscriptionResource:
    async def on_post(self, req, resp):
        data = await req.get_media()

        audio_file_name = data.get('audio_file_name')

        loop = asyncio.get_event_loop()
        language, transcript = await loop.run_in_executor(None, transcribe_audio, audio_file_name)
        
        print(transcript);
        print("\n...Transcription success...\n")

        resp.media = {'language': language, 'transcript': transcript}
        resp.status = falcon.HTTP_200

app = falcon.asgi.App()
app.add_route('/transcribe', TranscriptionResource())

if __name__ == '__main__':
    uvicorn.run(app, host='localhost', port=7558)
    print('Serving on http://localhost:7558...')