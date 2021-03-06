﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Api.SoapServer5 {
    using System.Runtime.Serialization;
    using System;
    
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.CollectionDataContractAttribute(Name="ArrayOfString", Namespace="http://tempuri.org/", ItemName="string")]
    [System.SerializableAttribute()]
    public class ArrayOfString : System.Collections.Generic.List<string> {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="NewImagePart", Namespace="http://tempuri.org/")]
    [System.SerializableAttribute()]
    public partial class NewImagePart : object, System.Runtime.Serialization.IExtensibleDataObject, System.ComponentModel.INotifyPropertyChanged {
        
        [System.NonSerializedAttribute()]
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string ImagePartInfoField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private byte[] newImageField;
        
        [global::System.ComponentModel.BrowsableAttribute(false)]
        public System.Runtime.Serialization.ExtensionDataObject ExtensionData {
            get {
                return this.extensionDataField;
            }
            set {
                this.extensionDataField = value;
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false)]
        public string ImagePartInfo {
            get {
                return this.ImagePartInfoField;
            }
            set {
                if ((object.ReferenceEquals(this.ImagePartInfoField, value) != true)) {
                    this.ImagePartInfoField = value;
                    this.RaisePropertyChanged("ImagePartInfo");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false)]
        public byte[] newImage {
            get {
                return this.newImageField;
            }
            set {
                if ((object.ReferenceEquals(this.newImageField, value) != true)) {
                    this.newImageField = value;
                    this.RaisePropertyChanged("newImage");
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void RaisePropertyChanged(string propertyName) {
            System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if ((propertyChanged != null)) {
                propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(ConfigurationName="SoapServer5.ProcessingSoap")]
    public interface ProcessingSoap {
        
        // CODEGEN: Generating message contract since element name AccessKey from namespace http://tempuri.org/ is not marked nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/CreateDir", ReplyAction="*")]
        Api.SoapServer5.CreateDirResponse CreateDir(Api.SoapServer5.CreateDirRequest request);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/CreateDir", ReplyAction="*")]
        System.Threading.Tasks.Task<Api.SoapServer5.CreateDirResponse> CreateDirAsync(Api.SoapServer5.CreateDirRequest request);
        
        // CODEGEN: Generating message contract since element name AccessKey from namespace http://tempuri.org/ is not marked nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/CreateXml", ReplyAction="*")]
        Api.SoapServer5.CreateXmlResponse CreateXml(Api.SoapServer5.CreateXmlRequest request);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/CreateXml", ReplyAction="*")]
        System.Threading.Tasks.Task<Api.SoapServer5.CreateXmlResponse> CreateXmlAsync(Api.SoapServer5.CreateXmlRequest request);
        
        // CODEGEN: Generating message contract since element name AccessKey from namespace http://tempuri.org/ is not marked nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/DownloadInstaPhotos", ReplyAction="*")]
        Api.SoapServer5.DownloadInstaPhotosResponse DownloadInstaPhotos(Api.SoapServer5.DownloadInstaPhotosRequest request);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/DownloadInstaPhotos", ReplyAction="*")]
        System.Threading.Tasks.Task<Api.SoapServer5.DownloadInstaPhotosResponse> DownloadInstaPhotosAsync(Api.SoapServer5.DownloadInstaPhotosRequest request);
        
        // CODEGEN: Generating message contract since element name AccessKey from namespace http://tempuri.org/ is not marked nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/ImageGenerate", ReplyAction="*")]
        Api.SoapServer5.ImageGenerateResponse ImageGenerate(Api.SoapServer5.ImageGenerateRequest request);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/ImageGenerate", ReplyAction="*")]
        System.Threading.Tasks.Task<Api.SoapServer5.ImageGenerateResponse> ImageGenerateAsync(Api.SoapServer5.ImageGenerateRequest request);
        
        // CODEGEN: Generating message contract since element name AccessKey from namespace http://tempuri.org/ is not marked nillable
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/XmlUpdate", ReplyAction="*")]
        Api.SoapServer5.XmlUpdateResponse XmlUpdate(Api.SoapServer5.XmlUpdateRequest request);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/XmlUpdate", ReplyAction="*")]
        System.Threading.Tasks.Task<Api.SoapServer5.XmlUpdateResponse> XmlUpdateAsync(Api.SoapServer5.XmlUpdateRequest request);
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class CreateDirRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="CreateDir", Namespace="http://tempuri.org/", Order=0)]
        public Api.SoapServer5.CreateDirRequestBody Body;
        
        public CreateDirRequest() {
        }
        
        public CreateDirRequest(Api.SoapServer5.CreateDirRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class CreateDirRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string AccessKey;
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=1)]
        public string UserName;
        
        public CreateDirRequestBody() {
        }
        
        public CreateDirRequestBody(string AccessKey, string UserName) {
            this.AccessKey = AccessKey;
            this.UserName = UserName;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class CreateDirResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="CreateDirResponse", Namespace="http://tempuri.org/", Order=0)]
        public Api.SoapServer5.CreateDirResponseBody Body;
        
        public CreateDirResponse() {
        }
        
        public CreateDirResponse(Api.SoapServer5.CreateDirResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class CreateDirResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=0)]
        public bool CreateDirResult;
        
        public CreateDirResponseBody() {
        }
        
        public CreateDirResponseBody(bool CreateDirResult) {
            this.CreateDirResult = CreateDirResult;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class CreateXmlRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="CreateXml", Namespace="http://tempuri.org/", Order=0)]
        public Api.SoapServer5.CreateXmlRequestBody Body;
        
        public CreateXmlRequest() {
        }
        
        public CreateXmlRequest(Api.SoapServer5.CreateXmlRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class CreateXmlRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string AccessKey;
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=1)]
        public string UserName;
        
        public CreateXmlRequestBody() {
        }
        
        public CreateXmlRequestBody(string AccessKey, string UserName) {
            this.AccessKey = AccessKey;
            this.UserName = UserName;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class CreateXmlResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="CreateXmlResponse", Namespace="http://tempuri.org/", Order=0)]
        public Api.SoapServer5.CreateXmlResponseBody Body;
        
        public CreateXmlResponse() {
        }
        
        public CreateXmlResponse(Api.SoapServer5.CreateXmlResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class CreateXmlResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=0)]
        public bool CreateXmlResult;
        
        public CreateXmlResponseBody() {
        }
        
        public CreateXmlResponseBody(bool CreateXmlResult) {
            this.CreateXmlResult = CreateXmlResult;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class DownloadInstaPhotosRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="DownloadInstaPhotos", Namespace="http://tempuri.org/", Order=0)]
        public Api.SoapServer5.DownloadInstaPhotosRequestBody Body;
        
        public DownloadInstaPhotosRequest() {
        }
        
        public DownloadInstaPhotosRequest(Api.SoapServer5.DownloadInstaPhotosRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class DownloadInstaPhotosRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string AccessKey;
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=1)]
        public string UserName;
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=2)]
        public Api.SoapServer5.ArrayOfString photos;
        
        public DownloadInstaPhotosRequestBody() {
        }
        
        public DownloadInstaPhotosRequestBody(string AccessKey, string UserName, Api.SoapServer5.ArrayOfString photos) {
            this.AccessKey = AccessKey;
            this.UserName = UserName;
            this.photos = photos;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class DownloadInstaPhotosResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="DownloadInstaPhotosResponse", Namespace="http://tempuri.org/", Order=0)]
        public Api.SoapServer5.DownloadInstaPhotosResponseBody Body;
        
        public DownloadInstaPhotosResponse() {
        }
        
        public DownloadInstaPhotosResponse(Api.SoapServer5.DownloadInstaPhotosResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class DownloadInstaPhotosResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=0)]
        public bool DownloadInstaPhotosResult;
        
        public DownloadInstaPhotosResponseBody() {
        }
        
        public DownloadInstaPhotosResponseBody(bool DownloadInstaPhotosResult) {
            this.DownloadInstaPhotosResult = DownloadInstaPhotosResult;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class ImageGenerateRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="ImageGenerate", Namespace="http://tempuri.org/", Order=0)]
        public Api.SoapServer5.ImageGenerateRequestBody Body;
        
        public ImageGenerateRequest() {
        }
        
        public ImageGenerateRequest(Api.SoapServer5.ImageGenerateRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class ImageGenerateRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string AccessKey;
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=1)]
        public string UserName;
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=2)]
        public byte[] ImagePart;
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=3)]
        public int x;
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=4)]
        public int y;
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=5)]
        public int width;
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=6)]
        public int height;
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=7)]
        public int PxFormat;
        
        public ImageGenerateRequestBody() {
        }
        
        public ImageGenerateRequestBody(string AccessKey, string UserName, byte[] ImagePart, int x, int y, int width, int height, int PxFormat) {
            this.AccessKey = AccessKey;
            this.UserName = UserName;
            this.ImagePart = ImagePart;
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.PxFormat = PxFormat;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class ImageGenerateResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="ImageGenerateResponse", Namespace="http://tempuri.org/", Order=0)]
        public Api.SoapServer5.ImageGenerateResponseBody Body;
        
        public ImageGenerateResponse() {
        }
        
        public ImageGenerateResponse(Api.SoapServer5.ImageGenerateResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class ImageGenerateResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public Api.SoapServer5.NewImagePart ImageGenerateResult;
        
        public ImageGenerateResponseBody() {
        }
        
        public ImageGenerateResponseBody(Api.SoapServer5.NewImagePart ImageGenerateResult) {
            this.ImageGenerateResult = ImageGenerateResult;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class XmlUpdateRequest {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="XmlUpdate", Namespace="http://tempuri.org/", Order=0)]
        public Api.SoapServer5.XmlUpdateRequestBody Body;
        
        public XmlUpdateRequest() {
        }
        
        public XmlUpdateRequest(Api.SoapServer5.XmlUpdateRequestBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class XmlUpdateRequestBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=0)]
        public string AccessKey;
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=1)]
        public string UserName;
        
        [System.Runtime.Serialization.DataMemberAttribute(EmitDefaultValue=false, Order=2)]
        public string key;
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=3)]
        public int _Value;
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=4)]
        public bool ValueArtir;
        
        public XmlUpdateRequestBody() {
        }
        
        public XmlUpdateRequestBody(string AccessKey, string UserName, string key, int _Value, bool ValueArtir) {
            this.AccessKey = AccessKey;
            this.UserName = UserName;
            this.key = key;
            this._Value = _Value;
            this.ValueArtir = ValueArtir;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.ServiceModel.MessageContractAttribute(IsWrapped=false)]
    public partial class XmlUpdateResponse {
        
        [System.ServiceModel.MessageBodyMemberAttribute(Name="XmlUpdateResponse", Namespace="http://tempuri.org/", Order=0)]
        public Api.SoapServer5.XmlUpdateResponseBody Body;
        
        public XmlUpdateResponse() {
        }
        
        public XmlUpdateResponse(Api.SoapServer5.XmlUpdateResponseBody Body) {
            this.Body = Body;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [System.Runtime.Serialization.DataContractAttribute(Namespace="http://tempuri.org/")]
    public partial class XmlUpdateResponseBody {
        
        [System.Runtime.Serialization.DataMemberAttribute(Order=0)]
        public bool XmlUpdateResult;
        
        public XmlUpdateResponseBody() {
        }
        
        public XmlUpdateResponseBody(bool XmlUpdateResult) {
            this.XmlUpdateResult = XmlUpdateResult;
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface ProcessingSoapChannel : Api.SoapServer5.ProcessingSoap, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class ProcessingSoapClient : System.ServiceModel.ClientBase<Api.SoapServer5.ProcessingSoap>, Api.SoapServer5.ProcessingSoap {
        
        public ProcessingSoapClient() {
        }
        
        public ProcessingSoapClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public ProcessingSoapClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public ProcessingSoapClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public ProcessingSoapClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        Api.SoapServer5.CreateDirResponse Api.SoapServer5.ProcessingSoap.CreateDir(Api.SoapServer5.CreateDirRequest request) {
            return base.Channel.CreateDir(request);
        }
        
        public bool CreateDir(string AccessKey, string UserName) {
            Api.SoapServer5.CreateDirRequest inValue = new Api.SoapServer5.CreateDirRequest();
            inValue.Body = new Api.SoapServer5.CreateDirRequestBody();
            inValue.Body.AccessKey = AccessKey;
            inValue.Body.UserName = UserName;
            Api.SoapServer5.CreateDirResponse retVal = ((Api.SoapServer5.ProcessingSoap)(this)).CreateDir(inValue);
            return retVal.Body.CreateDirResult;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Api.SoapServer5.CreateDirResponse> Api.SoapServer5.ProcessingSoap.CreateDirAsync(Api.SoapServer5.CreateDirRequest request) {
            return base.Channel.CreateDirAsync(request);
        }
        
        public System.Threading.Tasks.Task<Api.SoapServer5.CreateDirResponse> CreateDirAsync(string AccessKey, string UserName) {
            Api.SoapServer5.CreateDirRequest inValue = new Api.SoapServer5.CreateDirRequest();
            inValue.Body = new Api.SoapServer5.CreateDirRequestBody();
            inValue.Body.AccessKey = AccessKey;
            inValue.Body.UserName = UserName;
            return ((Api.SoapServer5.ProcessingSoap)(this)).CreateDirAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        Api.SoapServer5.CreateXmlResponse Api.SoapServer5.ProcessingSoap.CreateXml(Api.SoapServer5.CreateXmlRequest request) {
            return base.Channel.CreateXml(request);
        }
        
        public bool CreateXml(string AccessKey, string UserName) {
            Api.SoapServer5.CreateXmlRequest inValue = new Api.SoapServer5.CreateXmlRequest();
            inValue.Body = new Api.SoapServer5.CreateXmlRequestBody();
            inValue.Body.AccessKey = AccessKey;
            inValue.Body.UserName = UserName;
            Api.SoapServer5.CreateXmlResponse retVal = ((Api.SoapServer5.ProcessingSoap)(this)).CreateXml(inValue);
            return retVal.Body.CreateXmlResult;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Api.SoapServer5.CreateXmlResponse> Api.SoapServer5.ProcessingSoap.CreateXmlAsync(Api.SoapServer5.CreateXmlRequest request) {
            return base.Channel.CreateXmlAsync(request);
        }
        
        public System.Threading.Tasks.Task<Api.SoapServer5.CreateXmlResponse> CreateXmlAsync(string AccessKey, string UserName) {
            Api.SoapServer5.CreateXmlRequest inValue = new Api.SoapServer5.CreateXmlRequest();
            inValue.Body = new Api.SoapServer5.CreateXmlRequestBody();
            inValue.Body.AccessKey = AccessKey;
            inValue.Body.UserName = UserName;
            return ((Api.SoapServer5.ProcessingSoap)(this)).CreateXmlAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        Api.SoapServer5.DownloadInstaPhotosResponse Api.SoapServer5.ProcessingSoap.DownloadInstaPhotos(Api.SoapServer5.DownloadInstaPhotosRequest request) {
            return base.Channel.DownloadInstaPhotos(request);
        }
        
        public bool DownloadInstaPhotos(string AccessKey, string UserName, Api.SoapServer5.ArrayOfString photos) {
            Api.SoapServer5.DownloadInstaPhotosRequest inValue = new Api.SoapServer5.DownloadInstaPhotosRequest();
            inValue.Body = new Api.SoapServer5.DownloadInstaPhotosRequestBody();
            inValue.Body.AccessKey = AccessKey;
            inValue.Body.UserName = UserName;
            inValue.Body.photos = photos;
            Api.SoapServer5.DownloadInstaPhotosResponse retVal = ((Api.SoapServer5.ProcessingSoap)(this)).DownloadInstaPhotos(inValue);
            return retVal.Body.DownloadInstaPhotosResult;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Api.SoapServer5.DownloadInstaPhotosResponse> Api.SoapServer5.ProcessingSoap.DownloadInstaPhotosAsync(Api.SoapServer5.DownloadInstaPhotosRequest request) {
            return base.Channel.DownloadInstaPhotosAsync(request);
        }
        
        public System.Threading.Tasks.Task<Api.SoapServer5.DownloadInstaPhotosResponse> DownloadInstaPhotosAsync(string AccessKey, string UserName, Api.SoapServer5.ArrayOfString photos) {
            Api.SoapServer5.DownloadInstaPhotosRequest inValue = new Api.SoapServer5.DownloadInstaPhotosRequest();
            inValue.Body = new Api.SoapServer5.DownloadInstaPhotosRequestBody();
            inValue.Body.AccessKey = AccessKey;
            inValue.Body.UserName = UserName;
            inValue.Body.photos = photos;
            return ((Api.SoapServer5.ProcessingSoap)(this)).DownloadInstaPhotosAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        Api.SoapServer5.ImageGenerateResponse Api.SoapServer5.ProcessingSoap.ImageGenerate(Api.SoapServer5.ImageGenerateRequest request) {
            return base.Channel.ImageGenerate(request);
        }
        
        public Api.SoapServer5.NewImagePart ImageGenerate(string AccessKey, string UserName, byte[] ImagePart, int x, int y, int width, int height, int PxFormat) {
            Api.SoapServer5.ImageGenerateRequest inValue = new Api.SoapServer5.ImageGenerateRequest();
            inValue.Body = new Api.SoapServer5.ImageGenerateRequestBody();
            inValue.Body.AccessKey = AccessKey;
            inValue.Body.UserName = UserName;
            inValue.Body.ImagePart = ImagePart;
            inValue.Body.x = x;
            inValue.Body.y = y;
            inValue.Body.width = width;
            inValue.Body.height = height;
            inValue.Body.PxFormat = PxFormat;
            Api.SoapServer5.ImageGenerateResponse retVal = ((Api.SoapServer5.ProcessingSoap)(this)).ImageGenerate(inValue);
            return retVal.Body.ImageGenerateResult;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Api.SoapServer5.ImageGenerateResponse> Api.SoapServer5.ProcessingSoap.ImageGenerateAsync(Api.SoapServer5.ImageGenerateRequest request) {
            return base.Channel.ImageGenerateAsync(request);
        }
        
        public System.Threading.Tasks.Task<Api.SoapServer5.ImageGenerateResponse> ImageGenerateAsync(string AccessKey, string UserName, byte[] ImagePart, int x, int y, int width, int height, int PxFormat) {
            Api.SoapServer5.ImageGenerateRequest inValue = new Api.SoapServer5.ImageGenerateRequest();
            inValue.Body = new Api.SoapServer5.ImageGenerateRequestBody();
            inValue.Body.AccessKey = AccessKey;
            inValue.Body.UserName = UserName;
            inValue.Body.ImagePart = ImagePart;
            inValue.Body.x = x;
            inValue.Body.y = y;
            inValue.Body.width = width;
            inValue.Body.height = height;
            inValue.Body.PxFormat = PxFormat;
            return ((Api.SoapServer5.ProcessingSoap)(this)).ImageGenerateAsync(inValue);
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        Api.SoapServer5.XmlUpdateResponse Api.SoapServer5.ProcessingSoap.XmlUpdate(Api.SoapServer5.XmlUpdateRequest request) {
            return base.Channel.XmlUpdate(request);
        }
        
        public bool XmlUpdate(string AccessKey, string UserName, string key, int _Value, bool ValueArtir) {
            Api.SoapServer5.XmlUpdateRequest inValue = new Api.SoapServer5.XmlUpdateRequest();
            inValue.Body = new Api.SoapServer5.XmlUpdateRequestBody();
            inValue.Body.AccessKey = AccessKey;
            inValue.Body.UserName = UserName;
            inValue.Body.key = key;
            inValue.Body._Value = _Value;
            inValue.Body.ValueArtir = ValueArtir;
            Api.SoapServer5.XmlUpdateResponse retVal = ((Api.SoapServer5.ProcessingSoap)(this)).XmlUpdate(inValue);
            return retVal.Body.XmlUpdateResult;
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        System.Threading.Tasks.Task<Api.SoapServer5.XmlUpdateResponse> Api.SoapServer5.ProcessingSoap.XmlUpdateAsync(Api.SoapServer5.XmlUpdateRequest request) {
            return base.Channel.XmlUpdateAsync(request);
        }
        
        public System.Threading.Tasks.Task<Api.SoapServer5.XmlUpdateResponse> XmlUpdateAsync(string AccessKey, string UserName, string key, int _Value, bool ValueArtir) {
            Api.SoapServer5.XmlUpdateRequest inValue = new Api.SoapServer5.XmlUpdateRequest();
            inValue.Body = new Api.SoapServer5.XmlUpdateRequestBody();
            inValue.Body.AccessKey = AccessKey;
            inValue.Body.UserName = UserName;
            inValue.Body.key = key;
            inValue.Body._Value = _Value;
            inValue.Body.ValueArtir = ValueArtir;
            return ((Api.SoapServer5.ProcessingSoap)(this)).XmlUpdateAsync(inValue);
        }
    }
}
