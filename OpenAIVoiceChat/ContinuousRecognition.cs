using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAIVoiceChat;

public static class SpeechServicesWrapper
{
    static string speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
    static string speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");

    public async static Task ListenOnce(SpeechConfig speechConfig)
    {
        using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

        speechConfig.SpeechRecognitionLanguage = "en-US";

        //Console.WriteLine("Speak into your microphone.");
        //var result = await speechRecognizer.RecognizeOnceAsync();
        //Console.WriteLine($"RECOGNIZED: Text={result.Text}");
        var conversationEnded = false;
        Console.WriteLine("Azure OpenAI is listening. Say 'Stop' or press Ctrl-Z to end the conversation.");

        while (!conversationEnded)
        {
            // Get audio from the microphone and then send it to the TTS service.
            SpeechRecognitionResult speechRecognitionResult = await speechRecognizer.RecognizeOnceAsync();

            conversationEnded = ProcessSpeechResult(conversationEnded, speechRecognitionResult);
        }
    }

    private static bool ProcessSpeechResult(
        bool conversationEnded, 
        SpeechRecognitionResult speechRecognitionResult)
    {
        switch (speechRecognitionResult.Reason)
        {
            case ResultReason.RecognizedSpeech:
                if (speechRecognitionResult.Text == "Stop.")
                {
                    Console.WriteLine("Conversation ended.");
                    conversationEnded = true;
                }
                else
                {
                    Console.WriteLine($"Recognized speech: {speechRecognitionResult.Text}");
                    //await AskOpenAI(speechRecognitionResult.Text).ConfigureAwait(true);
                }
                break;
            case ResultReason.NoMatch:
                Console.WriteLine($"No speech could be recognized: ");
                break;
            case ResultReason.Canceled:
                var cancellationDetails = CancellationDetails.FromResult(speechRecognitionResult);
                Console.WriteLine($"Speech Recognition canceled: {cancellationDetails.Reason}");
                if (cancellationDetails.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"CANCELED: ErrorCode={cancellationDetails.ErrorCode}");
                    Console.WriteLine($"CANCELED: ErrorDetails={cancellationDetails.ErrorDetails}");
                    Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                }
                break;
        }

        return conversationEnded;
    }

    public async static Task ListenContinously(
        SpeechConfig speechConfig)
    {
        // Automatic language detection
        var autoDetectSourceLanguageConfig =
            AutoDetectSourceLanguageConfig.FromLanguages(new string[] { "en-US", "de-DE", "es-ES" });
        // Start and stop continuous recognition with Continuous LID
        speechConfig.SetProperty(PropertyId.SpeechServiceConnection_LanguageIdMode, "Continuous");

        using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        //using var audioConfig = AudioConfig.FromDefaultSpeakerOutput();
        using var speechRecognizerContinuously = 
            new SpeechRecognizer(
                speechConfig,
                autoDetectSourceLanguageConfig,
                audioConfig);

        var stopRecognition = new TaskCompletionSource<int>();

        speechRecognizerContinuously.Recognizing += (s, e) =>
        {
            //Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
            if (e.Result.Reason == ResultReason.RecognizingSpeech)
            {
                Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
                var autoDetectSourceLanguageResult = AutoDetectSourceLanguageResult.FromResult(e.Result);
                Console.WriteLine($"DETECTED: Language={autoDetectSourceLanguageResult.Language}");
            }
        };

        speechRecognizerContinuously.Recognized += async (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Console.WriteLine($"RECOGNIZED: Text={e.Result.Text}");
                var autoDetectSourceLanguageResult = AutoDetectSourceLanguageResult.FromResult(e.Result);
                Console.WriteLine($"DETECTED: Language={autoDetectSourceLanguageResult.Language}");

                if (e.Result.Text == "Stop.")
                {
                    Console.WriteLine("Conversation ended.");
                    await speechRecognizerContinuously.StopContinuousRecognitionAsync();
                }
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine($"NOMATCH: Speech could not be recognized.");
            }
        };

        speechRecognizerContinuously.Canceled += (s, e) =>
        {
            Console.WriteLine($"CANCELED: Reason={e.Reason}");

            if (e.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
            }

            stopRecognition.TrySetResult(0);
        };

        speechRecognizerContinuously.SessionStarted += (s, e) =>
        {
            Console.WriteLine("\n    Session started event.");
        };

        speechRecognizerContinuously.SessionStopped += (s, e) =>
        {
            Console.WriteLine("\n    Session stopped event.");
            stopRecognition.TrySetResult(0);
        };

        await speechRecognizerContinuously.StartContinuousRecognitionAsync();

        // Waits for completion. Use Task.WaitAny to keep the task rooted.
        Task.WaitAny(new[] { stopRecognition.Task });


        //Console.WriteLine($"RECOGNIZED: Text={result.Text}");
    }

    public async static Task ListenContinouslyWithTranslation(
        SpeechConfig speechConfig,
        string speechRegion,
        string speechKey)
    {
        // translation
        // Currently the v2 endpoint is required. In a future SDK release you won't need to set it.
        var endpointString = $"wss://{speechRegion}.stt.speech.microsoft.com/speech/universal/v2";
        var endpointUrl = new Uri(endpointString);
        var config = SpeechTranslationConfig.FromEndpoint(endpointUrl, speechKey);

        // Source language is required, but currently ignored. 
        string fromLanguage = "de-DE";
        config.SpeechRecognitionLanguage = fromLanguage;
        config.AddTargetLanguage("en");
        //config.AddTargetLanguage("de");
        //config.AddTargetLanguage("es");
        //config.AddTargetLanguage("it");

        // Start and stop continuous recognition with Continuous LID
        config.SetProperty(PropertyId.SpeechServiceConnection_LanguageIdMode, "Continuous");
        // Automatic language detection
        var autoDetectSourceLanguageConfig =
            AutoDetectSourceLanguageConfig.FromLanguages(new string[] { "en-US", "de-DE"});

        using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        //using var audioConfig = AudioConfig.FromDefaultSpeakerOutput();

        //using var speechRecognizerContinuously =
        //    new SpeechRecognizer(
        //        speechConfig,
        //        autoDetectSourceLanguageConfig,
        //        audioConfig);
        using var translationRecognizerContinuously =
            new TranslationRecognizer(
                config,
                autoDetectSourceLanguageConfig,
                audioConfig);

        var stopRecognition = new TaskCompletionSource<int>();

        //translationRecognizerContinuously.Recognizing += (s, e) =>
        //{
        //    var lidResult = 
        //        e.Result.Properties.GetProperty(PropertyId.SpeechServiceConnection_AutoDetectSourceLanguageResult);
        //    Console.WriteLine($"RECOGNIZING in '{lidResult}': Text={e.Result.Text}");

        //    //Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
        //    if (e.Result.Reason == ResultReason.RecognizingSpeech)
        //    {
        //        Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
        //        foreach (var element in e.Result.Translations)
        //        {
        //            Console.WriteLine($"    TRANSLATING into '{element.Key}': {element.Value}");
        //        }
                
        //        //var autoDetectSourceLanguageResult = AutoDetectSourceLanguageResult.FromResult(e.Result);
        //        //Console.WriteLine($"DETECTED: Language={autoDetectSourceLanguageResult.Language}");
        //    }
        //};

        translationRecognizerContinuously.Recognized += async (s, e) =>
        {
            if (e.Result.Reason == ResultReason.TranslatedSpeech)
            {
                var lidResult = e.Result.Properties.GetProperty(PropertyId.SpeechServiceConnection_AutoDetectSourceLanguageResult);

                if (!string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    Console.WriteLine($"RECOGNIZED in '{lidResult}': Text={e.Result.Text}");
                    foreach (var element in e.Result.Translations)
                    {
                        bool IsTranslationSameLanguage = lidResult.StartsWith(element.Key, StringComparison.OrdinalIgnoreCase);
                        if (!IsTranslationSameLanguage)
                        {
                            Console.WriteLine($"    TRANSLATED into '{element.Key}': {element.Value}");
                            if (element.Value == "Stop.")
                            {
                                Console.WriteLine("Conversation ended.");
                                await translationRecognizerContinuously.StopContinuousRecognitionAsync();
                            }
                        }
                    }                
                }
            }
            else if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Console.WriteLine($"RECOGNIZED: Text={e.Result.Text}");
                Console.WriteLine($"    Speech not translated.");
                //var autoDetectSourceLanguageResult = AutoDetectSourceLanguageResult.FromResult(e.Result);
                //Console.WriteLine($"DETECTED: Language={autoDetectSourceLanguageResult.Language}");

                if (e.Result.Text == "Stop.")
                {
                    Console.WriteLine("Conversation ended.");
                    await translationRecognizerContinuously.StopContinuousRecognitionAsync();
                }
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine($"NOMATCH: Speech could not be recognized.");
            }
        };

        translationRecognizerContinuously.Canceled += (s, e) =>
        {
            Console.WriteLine($"CANCELED: Reason={e.Reason}");

            if (e.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
            }

            stopRecognition.TrySetResult(0);
        };

        translationRecognizerContinuously.SessionStarted += (s, e) =>
        {
            Console.WriteLine("\n    Session started event.");
        };

        translationRecognizerContinuously.SessionStopped += (s, e) =>
        {
            Console.WriteLine("\n    Session stopped event.");
            stopRecognition.TrySetResult(0);
        };

        await translationRecognizerContinuously.StartContinuousRecognitionAsync();

        // Waits for completion. Use Task.WaitAny to keep the task rooted.
        Task.WaitAny(new[] { stopRecognition.Task });


        //Console.WriteLine($"RECOGNIZED: Text={result.Text}");
    }


}
