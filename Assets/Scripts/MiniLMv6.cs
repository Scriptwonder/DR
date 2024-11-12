using System.Collections.Generic;
using UnityEngine;
using Unity.Sentis;
using System.IO;
using System.Text;
using FF = Unity.Sentis.Functional;

/*
 *              Tiny Stories Inference Code
 *              ===========================
 *  
 *  Put this script on the Main Camera
 *  
 *  In Assets/StreamingAssets put:
 *  
 *  MiniLMv6.sentis
 *  vocab.txt
 * 
 *  Install package com.unity.sentis
 * 
 */


public class MiniLM : MonoBehaviour
{
    const BackendType backend = BackendType.GPUCompute;

    string string1 = "That is a happy person";          // similarity = 1

    //Choose a string to comapre string1  to:
    //string string2 = "That is a happy dog";             // similarity = 0.695
    string string2 = "That is a very happy person";   // similarity = 0.943
    //string string2 = "Today is a sunny day";          // similarity = 0.257

    //Special tokens
    const int START_TOKEN = 101; 
    const int END_TOKEN = 102; 

    //Store the vocabulary
    string[] tokens;

    const int FEATURES = 384; //size of feature space

    Worker engine, dotScore;

    void Start()
    {
        tokens = File.ReadAllLines(Application.streamingAssetsPath + "/vocab.txt");

        engine = CreateMLModel();

        dotScore = CreateDotScoreModel();

        var tokens1 = GetTokens(string1);
        var tokens2 = GetTokens(string2);

        using Tensor<float> embedding1 = GetEmbedding(tokens1);
        using Tensor<float> embedding2 = GetEmbedding(tokens2);

        float score = GetDotScore(embedding1, embedding2);

        Debug.Log("Similarity Score: " + score);
    }

    float GetDotScore(Tensor<float> A, Tensor<float> B)
    {
        var inputs = new Dictionary<string, Tensor>()
        {
            { "input_0", A },
            { "input_1", B }
        };
        var inputs1 = new Tensor[] { A, B };
        dotScore.Schedule(inputs1);
        var output = dotScore.PeekOutput() as Tensor<float>;
        output = output.ReadbackAndClone();
        return output[0];
    }

    Tensor<float> GetEmbedding(List<int> tokens)
    {
        int N = tokens.Count;
        using var input_ids = new Tensor<int>(new TensorShape(1, N), tokens.ToArray());
        using var token_type_ids = new Tensor<int>(new TensorShape(1, N), new int[N]);
        int[] mask = new int[N];
        for (int i = 0; i < mask.Length; i++)
        {
            mask[i] = 1;
        }
        using var attention_mask = new Tensor<int>(new TensorShape(1, N), mask);

        var inputs = new Dictionary<string, Tensor>
        {
            {"input_0", input_ids },
            {"input_1", attention_mask },
            {"input_2", token_type_ids}
        };
        var inputs1 = new Tensor[] { input_ids, attention_mask, token_type_ids };

        engine.Schedule(inputs1);

        //var output = engine.CopyOutput("output_0") as Tensor<float>;
        var output = engine.PeekOutput() as Tensor<float>;
        return output;
    }

    Worker CreateMLModel()
    {
        Model model = ModelLoader.Load(Application.streamingAssetsPath + "/MiniLMv6.sentis");

        // Model modelWithMeanPooling = Functional.Compile(
        //   (input_ids, attention_mask, token_type_ids) =>
        //   {
        //       var tokenEmbeddings = model.Forward(input_ids, attention_mask, token_type_ids)[0];
        //       return MeanPooling(tokenEmbeddings, attention_mask);
        //   },
        //   (model.inputs[0], model.inputs[1], model.inputs[2])
        // );

        var model1 = new FunctionalGraph();
        FunctionalTensor[] inputs = model1.AddInputs(model);
        FunctionalTensor[] outputs = Functional.Forward(model, inputs);
        //var modelWithMeanPooling = model1.Compile(MeanPooling(outputs[0], inputs[1]));
        FunctionalTensor meanPooled = MeanPooling(outputs[0], inputs[1]);
        var modelWithMeanPooling = model1.Compile(meanPooled);

        // var input1 = model1.AddInput(model.inputs[0].dataType, model.inputs[0].shape);
        // var input2 = model1.AddInput(model.inputs[1].dataType, model.inputs[1].shape);
        // var input3 = model1.AddInput(model.inputs[2].dataType, model.inputs[2].shape);
        //Model modelWithMeanPooling1 = model1.Compile(MeanPooling(Functional.Forward(model, input1, input2, input3)[0], input2));

        return new Worker(modelWithMeanPooling, backend);
    }

    //Get average of token embeddings taking into account the attention mask
    FunctionalTensor MeanPooling(FunctionalTensor tokenEmbeddings, FunctionalTensor attentionMask)
    {
        var mask = attentionMask.Unsqueeze(-1).BroadcastTo(new[] { FEATURES });     //shape=(1,N,FEATURES)
        var A = FF.ReduceSum(tokenEmbeddings * mask, 1, false);                     //shape=(1,FEATURES)       
        var B = A / (FF.ReduceSum(mask, 1, false) + 1e-9f);                         //shape=(1,FEATURES)
        var C = FF.Sqrt(FF.ReduceSum(FF.Square(B), 1, true));                       //shape=(1,FEATURES)
        return B / C;                                                               //shape=(1,FEATURES)
    }

    Worker CreateDotScoreModel()
    {
        // Model dotScoreModel = Functional.Compile(
        //     (input1, input2) => Functional.ReduceSum(input1 * input2, 1),
        //     (InputDef.Float(new TensorShape(1, FEATURES)),
        //     InputDef.Float(new TensorShape(1, FEATURES)))
        // );

        var model = new FunctionalGraph();
        var input1 = model.AddInput<float>(new TensorShape(1, FEATURES));
        var input2 = model.AddInput<float>(new TensorShape(1, FEATURES));
        //Model dotScoreModel = model.Compile(Functional.ReduceSum(input1 * input2, 1));
        FunctionalTensor reduce = Functional.ReduceSum(input1 * input2, 1);
        Model dotScoreModel = model.Compile(reduce);
        
        return new Worker(dotScoreModel, backend);
    }

    List<int> GetTokens(string text)
    {
        //split over whitespace
        string[] words = text.ToLower().Split(null);

        var ids = new List<int>
        {
            START_TOKEN
        };

        string s = "";

        foreach (var word in words)
        {
            int start = 0;
            for(int i = word.Length; i >= 0;i--)
            {
                string subword = start == 0 ? word.Substring(start, i) : "##" + word.Substring(start, i-start);
                int index = System.Array.IndexOf(tokens, subword);
                if (index >= 0)
                {
                    ids.Add(index);
                    s += subword + " ";
                    if (i == word.Length) break;
                    start = i;
                    i = word.Length + 1;
                }
            }
        }

        ids.Add(END_TOKEN);

        Debug.Log("Tokenized sentece = " + s);

        return ids;
    }

    private void OnDestroy()
    { 
        dotScore?.Dispose();
        engine?.Dispose();
    }

}
