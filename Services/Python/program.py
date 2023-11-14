from transcription import transcribe_audio

audio_file_name = "ChopSuey.mp3"
language, transcript = transcribe_audio(audio_file_name)
print(transcript)