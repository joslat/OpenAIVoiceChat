using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Azure;
using Azure.AI.OpenAI;
using static System.Environment;
using OpenAIVoiceChat;


/// <summary>
/// ASISTANT
/// 
/// TODO: recognice language continuously
/// https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/language-identification?tabs=once&pivots=programming-language-csharp
/// https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/language-identification?tabs=once&pivots=programming-language-csharp#candidate-languages
/// https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/language-identification?tabs=once&pivots=programming-language-csharp#at-start-and-continuous-language-identification
/// 
/// TODO: Be able to interrupt the assistant
/// https://stackoverflow.com/questions/62523344/how-to-stop-microsoft-cognitive-tts-audio-playing
/// https://learn.microsoft.com/en-us/javascript/api/microsoft-cognitiveservices-speech-sdk/speakeraudiodestination?view=azure-node-latest
/// https://learn.microsoft.com/en-us/dotnet/api/microsoft.cognitiveservices.speech.speechsynthesizer.speaktextasync?view=azure-dotnet
/// 
/// TODO: Select a default configuration to preload it
/// TODO: Recognice the speaker and use the same configuration for the same speaker // save some presets for him?
/// </summary>
class Program
{
    // This example requires environment variables named "OPEN_AI_KEY" and "OPEN_AI_ENDPOINT"
    // Your endpoint should look like the following https://YOUR_OPEN_AI_RESOURCE_NAME.openai.azure.com/
    static string openAIKey = Environment.GetEnvironmentVariable("OPEN_AI_KEY");
    static string openAIEndpoint = Environment.GetEnvironmentVariable("OPEN_AI_ENDPOINT");

    // Enter the deployment name you chose when you deployed the model.
    static string engine = "gpt4"; //gpt does not support completions

    // This example requires environment variables named "SPEECH_KEY" and "SPEECH_REGION"
    static string speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
    static string speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");
    static int _maxTokens = 100;
    static OpenAIModelInteractionTypes OpenAiInteractionModel = OpenAIModelInteractionTypes.ChatCompletion;
    static ChatCompletionsOptions chatCompletionsOptions;
    static CustomerProfileTypes customerProfileType = CustomerProfileTypes.FatherWithSpouseAndChildren;

    async static Task AskOpenAI(string prompt)
    {
        switch (OpenAiInteractionModel)
        {
            case OpenAIModelInteractionTypes.ChatCompletion:
                await AskOpenAIChatCompletions(prompt).ConfigureAwait(true);
                break;
            case OpenAIModelInteractionTypes.Completion:
                await AskOpenAICompletions(prompt).ConfigureAwait(true);
                break;
        }
    }

    async static Task AskOpenAIChatCompletions(string prompt)
    {
        OpenAIClient client = new(new Uri(openAIEndpoint), new AzureKeyCredential(openAIKey));
        ChatMessage chatMessage = new ChatMessage(ChatRole.User, prompt);
        chatCompletionsOptions.Messages.Add(chatMessage);

        Response<ChatCompletions> response = client.GetChatCompletions(
            deploymentOrModelName: engine,
            chatCompletionsOptions);
        string text =response.Value.Choices[0].Message.Content;
        
        Console.WriteLine($"Azure OpenAI response: {text}");

        await SpeakOutResponse(text);
    }

    // Prompts Azure OpenAI with a request and synthesizes the response.
    async static Task AskOpenAICompletions(string prompt)
    {
        // Ask Azure OpenAI
        OpenAIClient client = new(new Uri(openAIEndpoint), new AzureKeyCredential(openAIKey));
        var completionsOptions = new CompletionsOptions()
        {
            Prompts = { prompt },
            MaxTokens = _maxTokens,
        };

        Response<Completions> completionsResponse = client.GetCompletions(engine, completionsOptions);
        string text = completionsResponse.Value.Choices[0].Text.Trim();
        Console.WriteLine($"Azure OpenAI response: {text}");

        await SpeakOutResponse(text);
    }

    private static async Task SpeakOutResponse(string text)
    {
        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        // The language of the voice that speaks.
        speechConfig.SpeechSynthesisVoiceName = "en-US-JennyMultilingualNeural";
        var audioOutputConfig = AudioConfig.FromDefaultSpeakerOutput();

        using (var speechSynthesizer = new SpeechSynthesizer(speechConfig, audioOutputConfig))
        {
            var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(text).ConfigureAwait(true);



            if (speechSynthesisResult.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                Console.WriteLine($"Speech synthesized to speaker for text: [{text}]");
            }
            else if (speechSynthesisResult.Reason == ResultReason.Canceled)
            {
                var cancellationDetails = SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                Console.WriteLine($"Speech synthesis canceled: {cancellationDetails.Reason}");

                if (cancellationDetails.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"Error details: {cancellationDetails.ErrorDetails}");
                }
            }
        }
    }

    // Continuously listens for speech input to recognize and send as text to Azure OpenAI
    async static Task ChatWithOpenAI()
    {
        // Should be the locale for the speaker's language.
        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = "en-US";
        
        // Setting the profile
        customerProfileType = CustomerProfileTypes.FatherWithSpouseAndChildren;
        if (chatCompletionsOptions is null)
        {
            chatCompletionsOptions = new ChatCompletionsOptions()
            {
                Messages =
                {
                    new ChatMessage(ChatRole.System, "You are an expert coach specialized in insurance."),
                    //new ChatMessage(ChatRole.System, "You like to keep your responses short and concise."),
                    //new ChatMessage(ChatRole.System, "You are friendly and like to play with words sometimes, and also making occasionally jokes."),
                    //new ChatMessage(ChatRole.User, "Does Azure OpenAI support customer managed keys?"),
                    //new ChatMessage(ChatRole.Assistant, "Yes, customer managed keys are supported by Azure OpenAI."),
                    //new ChatMessage(ChatRole.User, "Do other Azure Cognitive Services support this too?"),
                },
                MaxTokens = _maxTokens
            };
        }

        CustomerProfileManager.PrepareConversationCoachForProfile(customerProfileType, chatCompletionsOptions);

        using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        var conversationEnded = false;

        while (!conversationEnded)
        {
            Console.WriteLine("Azure OpenAI is listening. Say 'Stop' or press Ctrl-Z to end the conversation.");

            // Get audio from the microphone and then send it to the TTS service.
            var speechRecognitionResult = await speechRecognizer.RecognizeOnceAsync();

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
                        await AskOpenAI(speechRecognitionResult.Text).ConfigureAwait(true);
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
                        Console.WriteLine($"Error details={cancellationDetails.ErrorDetails}");
                    }
                    break;
            }
        }
    }

    async static Task Main(string[] args)
    {
        try
        {
            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            //speechConfig.SpeechRecognitionLanguage = "en-US";

            //await SpeechServicesWrapper.ListenOnce(speechConfig);
            await SpeechServicesWrapper.ListenContinouslyWithTranslation(speechConfig, speechRegion, speechKey);

            //await ChatWithOpenAI().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}