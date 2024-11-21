using UnityEngine;
using OpenAI;
using OpenAI.Assistants;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenAI.Chat;
using OpenAI.Models;
using System.Linq;

public class LLManager : MonoBehaviour
{
    private OpenAIAuthentication auth;

    private Model model;

    [SerializeField]
    private string currentContext;
    [SerializeField]
    private  TextAsset labelsAsset;

    private string[] labelIndexes;

    List<Message> messages = new List<Message>();

    List<Message> outputMessages = new List<Message>();

    private static LLManager instance;

    

    public static LLManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<LLManager>();
                
                // Create new instance if none exists
                if (instance == null)
                {
                    GameObject go = new GameObject("LLManager");
                    instance = go.AddComponent<LLManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        //var authToken = await LoginAsync();
        //auth = new OpenAIAuthentication("yourownkey");

    
    }

    void Start()
    {
        //Setup();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Setup()
    {
        var api = new OpenAIClient(auth);
        model = await api.ModelsEndpoint.GetModelDetailsAsync("gpt-4o");
        var assistant = await api.AssistantsEndpoint.CreateAssistantAsync(
            new CreateAssistantRequest(
                model,
                name: "DR Assistant",
                description: "DR Assistant",
                instructions: "You are a assistant that will help user with identifying the objects that are not related with the input context."
            )
        );
        StartNewMessage();
        messages.Add(new Message(Role.System, "You are a helpful assistant that will help user with identifying the objects that are not related with the input context."));
        InputMessage("Given the following labels file, please identify the objects that are strongly related with the input context.");
        //InputMessage("For example, for a user that is trying to focus on work/study, the objects that are related to the context would be: computer, desk, chair, mouse, keyboard, etc.");
        InputMessage(labelsAsset.text);
        //InputMessage(labelsAsset.text.ToString());
        //InputMessage("Do separate the objects only with a line separator, some objects may contain two words, but they are in the same line. Use 1-index instead of 0-index");
        InputMessage("The Input Context is: " + currentContext);
        InputMessage("Now for the objects that are in the labels file, output their indexes");
        //InputMessage("Now Do not give words, but just line numbers that represent the indexes of these items in the file.");
        await SendMessage();
        InputMessage(outputMessages.Last().ToString());
        InputMessage("Now edit the previous message with only the indexes, no words in the format of each divided by ', '");
        await SendMessage();
        DissectMessage(outputMessages.Last());
    }

    //every message will go through this method
    public async Task SendMessage()
    {
        var api = new OpenAIClient(auth);
        var chatRequest = new ChatRequest(messages, model);
        var response = await api.ChatEndpoint.GetCompletionAsync(chatRequest);
        var choice = response.FirstChoice;
        Debug.Log($"[{choice.Index}] {choice.Message.Role}: {choice.Message} | Finish Reason: {choice.FinishReason}");
        outputMessages.Add(choice.Message);
    }

    public void DissectMessage(Message message) {
        labelIndexes = message.ToString().Split(", ");
        foreach (string labelIndex in labelIndexes) {
            Debug.Log(labelIndex);
        }
    }

    public void InputMessage(string message) {
        messages.Add(new Message(Role.User, message));
    }

    public void StartNewMessage() {
        messages.Clear();
        
    }

    public bool FindLabelIndex(string labelIndex) {
        if (labelIndexes == null) {
            return false;
        }
        foreach (string label in labelIndexes) {
            if (label == labelIndex) {
                return true;
            }
        }
        return false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
