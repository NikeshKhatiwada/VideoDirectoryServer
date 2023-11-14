import whisper
# import argparse

model = whisper.load_model("small")

def convert_time_format(input_time):
    seconds = int(input_time)
    milliseconds = int((input_time - seconds) * 1000)
    
    minutes, seconds = divmod(seconds, 60)
    hours, minutes = divmod(minutes, 60)

    time_string = f"{hours:02}:{minutes:02}:{seconds:02},{milliseconds:03}"
    return time_string

def transcribe_audio(audio_file_name):
    audio_file_path = "Audios/" + audio_file_name;

    result = model.transcribe(audio_file_path)

    language = result["language"]
    sorted_segments = sorted(result["segments"], key=lambda x: x["start"])
    transcript = ""
    
    i = 1;
    for segment in sorted_segments:
        start_time = segment["start"]
        end_time = segment["end"]
        text = segment["text"]
        text = text.strip()
    
        transcript += f"{i}\n"
        i += 1
        transcript += f"{convert_time_format(start_time)} --> {convert_time_format(end_time)}\n"
        transcript += f"{text}\n"
        transcript += "\n"

    print(language + "..." + transcript)
    return language, transcript

# if __name__ == "__main__":
#     parser = argparse.ArgumentParser(description="Transcribe audio and format time.")
#     parser.add_argument("audio_file_name", help="Name of the audio file to transcribe")

#     args = parser.parse_args()

#     language, transcript = transcribe_audio(args.audio_file_name)
#     print(f"Language: {language}")
#     print(transcript)

__all__ = ['transcribe_audio']