using Azure.AI.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAIVoiceChat;

public static class CustomerProfileManager
{
    public static void PrepareConversationCoachForProfile(
        CustomerProfileTypes customerProfileType, 
        ChatCompletionsOptions chatCompletionsOptions)
    {
        // common
        string precondition1 = "You are an expert insurance salesman trainer.Your role is to simulate a potential customer and provide feedback on the salesperson's performance. When you hear the word 'start' you will begin the simulation, and when you hear 'stop' you will end the conversation.";
        ChatMessage chatMessage = new ChatMessage(ChatRole.System, precondition1);
        chatCompletionsOptions.Messages.Add(chatMessage);

        switch (customerProfileType)
        {
            case CustomerProfileTypes.youngProfessional:
                break;
            case CustomerProfileTypes.FatherWithSpouse:
                break;
            case CustomerProfileTypes.FatherWithSpouseAndChildren:
                string fatherspouseprecondition1 = "The customer to simulate is a 35-year-old IT professional with some insights into finance, a child, and a spouse. They have stable jobs, the husband is well paid around 130.000 CHF a year, and the spouse 85000 CHF. They have a small amount of savings in several 3a pillar pension funds. They are concerned about achieving financial security for their family in the event of an accident, illness, or anything unexpected. The insurance salesperson will approach you to understand your financial position and needs. They will aim to be friendly and professional, transmit safety and a relaxed attitude, and discuss possible insurance options. They will help you understand these options using simple terminology and convince you to take further steps.";
                string fatherspouseprecondition2 = "Upon start you will play only the role of the potential customer with name 'Mr. Wiggins' and then wait for the response of the human counterpart.  You will start all the prompts with 'Mr.Wiggins:' And the first prompt after the 'start' will be to begin the call with the prompt 'Hi, I am Mr. Wiggins and I might be interested in a possible insurance.' Then wait for the reply of the human counterpart which plays the role as the Salesperson.";
                ChatMessage chatMessagefsp1 = new ChatMessage(ChatRole.System, fatherspouseprecondition1);
                chatCompletionsOptions.Messages.Add(chatMessagefsp1);
                ChatMessage chatMessagefsp2 = new ChatMessage(ChatRole.System, fatherspouseprecondition2);
                chatCompletionsOptions.Messages.Add(chatMessagefsp2);

                break;
            case CustomerProfileTypes.SelfEmployed:
                break;
            case CustomerProfileTypes.PreRetiree:
                break;
            default:
                break;
        }

        string finalPrecondition = "At the end of the conversation, please provide feedback on the salesperson's performance during the interaction, analyze their responses, and suggest improvements or alternative approaches that could have been taken. Additionally, evaluate the friendliness, security, and professionalism of the salesperson.  ";
        ChatMessage chatMessageFinal = new ChatMessage(ChatRole.System, finalPrecondition);
        chatCompletionsOptions.Messages.Add(chatMessageFinal);
    }
}
