/* 
Licensed to the Apache Software Foundation (ASF) under one
or more contributor license agreements.  See the NOTICE file
distributed with this work for additional information
regarding copyright ownership.  The ASF licenses this file
to you under the Apache License, Version 2.0 (the
"License"); you may not use this file except in compliance
with the License.  You may obtain a copy of the License at

  http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing,
software distributed under the License is distributed on an
"AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
KIND, either express or implied.  See the License for the
specific language governing permissions and limitations
under the License.
*/
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EtwPilot.Model
{
    //
    // The format of the JSON file should conform to the onnxruntime-genai config format, eg:
    //      https://huggingface.co/microsoft/Phi-3-mini-128k-instruct-onnx/tree/main/cuda/cuda-int4-rtn-block-32/genai_config.json
    // See https://onnxruntime.ai/docs/genai/reference/config.html
    //
    public class OnnxGenAIConfigModel
    {
        [JsonProperty(PropertyName = "model")]
        public OnnxGenAIModelOptionsModel ModelOptions { get; set; }

        [JsonProperty(PropertyName = "search")]
        public OnnxGenAISearchOptionsModel SearchOptions { get; set; }

        public OnnxGenAIConfigModel()
        {
            ModelOptions = new OnnxGenAIModelOptionsModel();
            SearchOptions = new OnnxGenAISearchOptionsModel();
        }
    }

    //
    // See https://onnxruntime.ai/docs/genai/reference/config.html
    //
    public class OnnxGenAIModelOptionsModel
    {
        //
        // The type of model. Can be phi, llama or gpt.
        //
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        //
        // The maximum length of sequence that the model can process
        //
        [JsonProperty(PropertyName = "context_length")]
        public int ContextLength { get; set; }

        public OnnxGenAIModelOptionsModel()
        {

        }
    }

    //
    // See: https://onnxruntime.ai/docs/genai/reference/config.html
    //
    public class OnnxGenAISearchOptionsModel : INotifyPropertyChanged
    {
        private double _topK = 50;
        private double _topP = 0.9f;
        private double _temperature = 1;
        private double _repetitionPenalty = 1;
        private bool _pastPresentShareBuffer = false;
        private double _numReturnSequences = 1;
        private double _numBeams = 1;
        private double _noRepeatNgramSize = 0;
        private double _minLength = 0;
        private double _maxLength = 200;
        private double _lengthPenalty = 1;
        private bool _earlyStopping = true;
        private bool _doSample = false;
        private double _diversityPenalty = 0;

        /// <summary>
        /// Only includes tokens that do fall within the list of the K most probable tokens. 
        /// Range is 1 to the vocabulary size.
        /// </summary>
        [JsonProperty(PropertyName = "top_k")]
        public double TopK
        {
            get { return _topK; }
            set { _topK = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Only includes the most probable tokens with probabilities that add up to P or higher. 
        /// Defaults to 1, which includes all of the tokens. Range is 0 to 1, exclusive of 0.
        /// </summary>
        [JsonProperty(PropertyName = "top_p")]
        public double TopP
        {
            get { return _topP; }
            set { _topP = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// The temperature value scales the scores of each token so that lower a temperature 
        /// value leads to a sharper distribution.
        /// </summary>
        [JsonProperty(PropertyName = "temperature")]
        public double Temperature
        {
            get { return _temperature; }
            set { _temperature = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Discounts the scores of previously generated tokens if set to a value greater 
        /// than 1. Defaults to 1.
        /// </summary>
        [JsonProperty(PropertyName = "repetition_penalty")]
        public double RepetitionPenalty
        {
            get { return _repetitionPenalty; }
            set { _repetitionPenalty = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        ///  If set to true, the past and present buffer are shared for efficiency.
        /// </summary>
        [JsonProperty(PropertyName = "past_present_share_buffer")]
        public bool PastPresentShareBuffer
        {
            get { return _pastPresentShareBuffer; }
            set { _pastPresentShareBuffer = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// The number of sequences to generate. Returns the sequences with the highest scores in order.
        /// </summary>
        [JsonProperty(PropertyName = "num_return_sequences")]
        public double NumReturnSequences
        {
            get { return _numReturnSequences; }
            set { _numReturnSequences = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        ///  The number of beams to apply when generating the output sequence using beam search. 
        ///  If num_beams=1, then generation is performed using greedy search. If num_beans > 1,
        ///  then generation is performed using beam search.
        /// </summary>
        [JsonProperty(PropertyName = "num_beams")]
        public double NumBeams
        {
            get { return _numBeams; }
            set { _numBeams = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Currently not supported.
        /// </summary>
        [JsonProperty(PropertyName = "no_repeat_ngram_size")]
        public double NoRepeatNgramSize
        {
            get { return _noRepeatNgramSize; }
            set { _noRepeatNgramSize = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// The minimum length that the model will generate.
        /// </summary>
        [JsonProperty(PropertyName = "min_length")]
        public double MinLength
        {
            get { return _minLength; }
            set { _minLength = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// The maximum length that the model will generate.
        /// </summary>
        [JsonProperty(PropertyName = "max_length")]
        public double MaxLength
        {
            get { return _maxLength; }
            set { _maxLength = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Controls the length of the output generated. Value less than 1 encourages the generation
        /// to produce shorter sequences. Values greater than 1 encourages longer sequences. Defaults to 1.
        /// </summary>
        [JsonProperty(PropertyName = "length_penalty")]
        public double LengthPenalty
        {
            get { return _lengthPenalty; }
            set { _lengthPenalty = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Currently not supported.
        /// </summary>
        [JsonProperty(PropertyName = "diversity_penalty")]
        public double DiversityPenalty
        {
            get { return _diversityPenalty; }
            set { _diversityPenalty = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Whether to stop the beam search when at least num_beams sentences are 
        /// finished per batch or not. Defaults to false.
        /// </summary>
        [JsonProperty(PropertyName = "early_stopping")]
        public bool EarlyStopping
        {
            get { return _earlyStopping; }
            set { _earlyStopping = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Enables Top P / Top K generation. When set to true, generation uses the configured top_p and top_k values.
        /// When set to false, generation uses beam search or greedy search.
        /// </summary>
        [JsonProperty(PropertyName = "do_sample")]
        public bool DoSample
        {
            get { return _doSample; }
            set { _doSample = value; NotifyPropertyChanged(); }
        }

        public OnnxGenAISearchOptionsModel()
        {

        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        #endregion
    }
}
