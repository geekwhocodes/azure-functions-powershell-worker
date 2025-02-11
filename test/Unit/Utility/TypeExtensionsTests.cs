//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Management.Automation;
using System.Text;

using Google.Protobuf;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    public class TypeExtensionsTests
    {
        #region TypedDataToObject

        [Fact]
        public void TestTypedDataToObjectHttpRequestContextBasic()
        {
            var method = "Get";
            var url = "https://example.com";

            var input = new TypedData
            {
                Http = new RpcHttp
                {
                    Method = method,
                    Url = url
                }
            };

            var httpRequestContext = (HttpRequestContext)input.ToObject();

            Assert.Equal(httpRequestContext.Method, method);
            Assert.Equal(httpRequestContext.Url, url);
            Assert.Null(httpRequestContext.Body);
            Assert.Null(httpRequestContext.RawBody);
            Assert.Empty(httpRequestContext.Headers);
            Assert.Empty(httpRequestContext.Params);
            Assert.Empty(httpRequestContext.Query);
        }

        [Fact]
        public void TestTypedDataToObjectHttpRequestContextWithUrlData()
        {
            var method = "Get";
            var url = "https://example.com";
            var key = "foo";
            var value = "bar";

            var input = new TypedData
            {
                Http = new RpcHttp
                {
                    Method = method,
                    Url = url
                }
            };
            input.Http.Headers.Add(key, value);
            input.Http.Params.Add(key, value);
            input.Http.Query.Add(key, value);

            var httpRequestContext = (HttpRequestContext)input.ToObject();

            Assert.Equal(httpRequestContext.Method, method);
            Assert.Equal(httpRequestContext.Url, url);
            Assert.Null(httpRequestContext.Body);
            Assert.Null(httpRequestContext.RawBody);

            Assert.Single(httpRequestContext.Headers);
            Assert.Single(httpRequestContext.Params);
            Assert.Single(httpRequestContext.Query);

            Assert.Equal(httpRequestContext.Headers[key], value);
            Assert.Equal(httpRequestContext.Params[key], value);
            Assert.Equal(httpRequestContext.Query[key], value);
        }

        [Fact]
        public void TestTypedDataToObjectHttpRequestContextBodyData()
        {
            var method = "Get";
            var url = "https://example.com";
            var data = "Hello World";

            // RawBody from the input object is not used anymore
            // (see the "RpcHttpBodyOnly" capability for more details).
            var rawData = "does not matter";

            var input = new TypedData
            {
                Http = new RpcHttp
                {
                    Method = method,
                    Url = url,
                    Body = new TypedData
                    {
                        String = data
                    },
                    RawBody = new TypedData
                    {
                        String = rawData
                    }
                }
            };

            var httpRequestContext = (HttpRequestContext)input.ToObject();

            Assert.Equal(method, httpRequestContext.Method);
            Assert.Equal(url, httpRequestContext.Url);
            Assert.Equal(data, httpRequestContext.Body);
            Assert.Empty(httpRequestContext.Headers);
            Assert.Empty(httpRequestContext.Params);
            Assert.Empty(httpRequestContext.Query);
        }

        [Fact]
        public void TestTypedDataToObjectHttpRequestContextBodyData_String()
        {
            var data = "Hello World";

            var input = new TypedData
            {
                Http = new RpcHttp
                {
                    Method = "Get",
                    Url = "https://example.com",
                    Body = new TypedData
                    {
                        String = data
                    }
                }
            };

            var httpRequestContext = (HttpRequestContext)input.ToObject();

            Assert.Equal(data, httpRequestContext.Body);
            Assert.Equal(data, httpRequestContext.RawBody);
        }

        [Fact]
        public void TestTypedDataToObjectHttpRequestContextBodyData_Bytes()
        {
            var data = "Hello World";

            var input = new TypedData
            {
                Http = new RpcHttp
                {
                    Method = "Get",
                    Url = "https://example.com",
                    Body = new TypedData
                    {
                        Bytes = ByteString.CopyFromUtf8(data)
                    }
                }
            };

            var httpRequestContext = (HttpRequestContext)input.ToObject();

            Assert.Equal(Encoding.UTF8.GetBytes(data), httpRequestContext.Body);
            Assert.Equal(data, httpRequestContext.RawBody);
        }

        [Theory]
        [InlineData("text/plain", "Hello World")]
        [InlineData("text/plain", "{\"Key\":\"Value\"}")]
        [InlineData("text/something", "{\"Key\":\"Value\"}")]
        [InlineData("application/something", "{\"Key\":\"Value\"}")]
        [InlineData("application/json-patch+json", "{\"Key\":\"Value\"}")]
        [InlineData("anything/else", "{\"Key\":\"Value\"}")]
        [InlineData(";", "{\"Key\":\"Value\"}")]
        public void TestTypedDataToObjectHttpRequestContextBodyData_DoesNotDeserializeStringFromJson(string contentType, string stringData)
        {
            var input = new TypedData
            {
                Http = new RpcHttp
                {
                    Headers = { { "content-type", contentType } },
                    Body = new TypedData { String = stringData }
                }
            };

            var httpRequestContext = (HttpRequestContext)input.ToObject();

            Assert.Equal(stringData, httpRequestContext.Body);
        }

        [Theory]
        [InlineData("application/json", "{\"Key\":\"Value\"}")]
        [InlineData("application/json; charset=utf-8", "{\"Key\":\"Value\"}")]
        [InlineData("Application/JSON", "{\"Key\":\"Value\"}")]
        [InlineData(null, "{\"Key\":\"Value\"}")]
        public void TestTypedDataToObjectHttpRequestContextBodyData_DoesDeserializeStringFromJson(string contentType, string stringData)
        {
            var rpcHttp = contentType == null
                            ? new RpcHttp
                                    {
                                        Body = new TypedData { String = stringData }
                                    }
                            : new RpcHttp
                                    {
                                        Headers = { { "content-type", contentType } },
                                        Body = new TypedData { String = stringData }
                                    };

            var input = new TypedData
            {
                Http = rpcHttp
            };

            var httpRequestContext = (HttpRequestContext)input.ToObject();

            var expected = JsonConvert.DeserializeObject<Hashtable>(stringData);
            var actual = (Hashtable)httpRequestContext.Body;
            Assert.Equal(expected["Key"], actual["Key"]);
        }

        [Fact]
        public void TestTypedDataToObjectString()
        {
            var data = "Hello World";

            var input = new TypedData { String = data };
            var expected = data;

            Assert.Equal(expected, (string)input.ToObject());
        }

        [Fact]
        public void TestTypedDataToObjectStringAsJson_Deserialize()
        {
            var data = "{\"Foo\":\"Bar\"}";

            var input = new TypedData { String = data };
            var expected = JsonConvert.DeserializeObject<Hashtable>(data);
            var actual = (Hashtable)input.ToObject();
            Assert.Equal((string)expected["Foo"], (string)actual["Foo"]);
        }

        [Fact]
        public void TestTypedDataToObjectStringAsJson_DoNotDeserialize()
        {
            var data = "{\"Foo\":\"Bar\"}";

            var input = new TypedData { String = data };
            var expected = data;
            var actual = (string)input.ToObject(convertFromJsonIfValidJson: false);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestTypedDataToObjectInt()
        {
            long data = 2;

            var input = new TypedData { Int = data };
            var expected = data;

            Assert.Equal(expected, (long)input.ToObject());
        }

        [Fact]
        public void TestTypedDataToObjectDouble()
        {
            var data = 2.2;

            var input = new TypedData { Double = data };
            var expected = data;

            Assert.Equal(expected, (double)input.ToObject());
        }

        [Fact]
        public void TestTypedDataToObjectJson()
        {
            // A dictionary with simple key/value
            var data = "{\"Foo\":\"Bar\"}";

            var input = new TypedData { Json = data };
            var expected = JsonConvert.DeserializeObject<Hashtable>(data);
            var actual = (Hashtable)input.ToObject();
            Assert.Equal((string)expected["Foo"], (string)actual["Foo"]);
        }

        [Fact]
        public void TestTypedDataToObjectJson2()
        {
            // A dictionary with an array value
            var data = "{\"Foo\":[\"Bar\",\"Zoo\"]}";

            var input = new TypedData { Json = data };
            var actual = input.ToObject();
            Assert.IsType<Hashtable>(actual);
            var actualHash = (Hashtable)actual;
            Assert.IsType<object[]>(actualHash["Foo"]);
            var actualArray = (object[])actualHash["Foo"];
            Assert.Equal(2, actualArray.Length);
            Assert.Equal("Bar", actualArray[0]);
            Assert.Equal("Zoo", actualArray[1]);
        }

        [Fact]
        public void TestTypedDataToObjectJson3()
        {
            // A dictionary with a dictionary value
            var data = "{\"Foo\":{\"Bar\":\"Zoo\"}}";

            var input = new TypedData { Json = data };
            var actual = input.ToObject();
            Assert.IsType<Hashtable>(actual);
            var actualHash = (Hashtable)actual;
            Assert.IsType<Hashtable>(actualHash["Foo"]);
            var nestedHash = (Hashtable)actualHash["Foo"];
            Assert.Single(nestedHash);
            Assert.Equal("Zoo", nestedHash["Bar"]);
        }

        [Fact]
        public void TestTypedDataToObjectJson4()
        {
            // An array with simple value
            var data = "[\"Foo\",\"Bar\"]";

            var input = new TypedData { Json = data };
            var actual = input.ToObject();
            Assert.IsType<object[]>(actual);
            var actualArray = (object[])actual;
            Assert.Equal(2, actualArray.Length);
            Assert.Equal("Foo", actualArray[0]);
            Assert.Equal("Bar", actualArray[1]);
        }

        [Fact]
        public void TestTypedDataToObjectJson5()
        {
            // An array with an array value
            var data = "[\"Foo\", [\"Bar\", \"Zoo\"]]";

            var input = new TypedData { Json = data };
            var actual = input.ToObject();
            Assert.IsType<object[]>(actual);
            var actualArray = (object[])actual;
            Assert.Equal(2, actualArray.Length);
            Assert.Equal("Foo", actualArray[0]);

            Assert.IsType<object[]>(actualArray[1]);
            var nestedArray = (object[])actualArray[1];
            Assert.Equal(2, nestedArray.Length);
            Assert.Equal("Bar", nestedArray[0]);
            Assert.Equal("Zoo", nestedArray[1]);
        }

        [Fact]
        public void TestTypedDataToObjectJson6()
        {
            // An array with an array value
            var data = "[\"Foo\", {\"Bar\":\"Zoo\"}]";

            var input = new TypedData { Json = data };
            var actual = input.ToObject();
            Assert.IsType<object[]>(actual);
            var actualArray = (object[])actual;
            Assert.Equal(2, actualArray.Length);
            Assert.Equal("Foo", actualArray[0]);

            Assert.IsType<Hashtable>(actualArray[1]);
            var nestedHash = (Hashtable)actualArray[1];
            Assert.Single(nestedHash);
            Assert.Equal("Zoo", nestedHash["Bar"]);
        }

        [Fact]
        public void TestTypedDataToObjectBytes()
        {
            var data = ByteString.CopyFromUtf8("Hello World");

            var input = new TypedData { Bytes = data };
            var expected = data.ToByteArray();

            Assert.Equal(expected, (byte[])input.ToObject());
        }

        [Fact]
        public void TestTypedDataToObjectStream()
        {
            var data = ByteString.CopyFromUtf8("Hello World");

            var input = new TypedData { Stream = data };
            var expected = data.ToByteArray();

            Assert.Equal(expected, (byte[])input.ToObject());
        }
        #endregion

        #region ExceptionToRpcException
        [Fact]
        public void TestExceptionToRpcExceptionBasic()
        {
            var data = "bad";

            var input = new Exception(data);
            var expected = new RpcException
            {
                Message = "bad"
            };

            Assert.Equal(expected, input.ToRpcException());
        }

        [Fact]
        public void TestExceptionToRpcExceptionExtraData()
        {
            var data = "bad";

            var input = new Exception(data);
            input.Source = data;

            var expected = new RpcException
            {
                Message = data,
                Source = data
            };

            Assert.Equal(expected, input.ToRpcException());
        }
        #endregion

        #region ObjectToTypedData
        [Fact]
        public void TestObjectToTypedDataRpcHttpBasic()
        {
            var data = "Hello World";

            var input = new HttpResponseContext
            {
                Body = data
            };
            var expected = new TypedData
            {
                Http = new RpcHttp
                {
                    StatusCode = "200",
                    Body = new TypedData { String = data },
                    Headers = { { "content-type", "text/plain" } }
                }
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataRpcHttpContentTypeSet()
        {
            var data = "<html></html>";

            var input = new HttpResponseContext
            {
                Body = data,
                ContentType = "text/html"
            };
            var expected = new TypedData
            {
                Http = new RpcHttp
                {
                    StatusCode = "200",
                    Body = new TypedData { String = data },
                    Headers = { { "content-type", "text/html" } }
                }
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataRpcHttpContentTypeInHeader()
        {
            var data = "<html></html>";

            var input = new HttpResponseContext
            {
                Body = data,
                Headers = new Hashtable { { "content-type", "text/html" } }
            };
            var expected = new TypedData
            {
                Http = new RpcHttp
                {
                    StatusCode = "200",
                    Body = new TypedData { String = data },
                    Headers = { { "content-type", "text/html" } }
                }
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataRpcHttpStatusCodeString()
        {
            var data = "Hello World";

            var input = new HttpResponseContext
            {
                Body = data,
                StatusCode = HttpStatusCode.Created
            };
            var expected = new TypedData
            {
                Http = new RpcHttp
                {
                    StatusCode = "201",
                    Body = new TypedData { String = data },
                    Headers = { { "content-type", "text/plain" } }
                }
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedData_WhenBodyIsJsonAndNoContentType_AddsApplicationJsonContentTypeHeader()
        {
            var input = new HttpResponseContext
            {
                Body = new TypedData { Json = "{\"Hello\":\"World\"}" }
            };

            Assert.Equal("application/json", input.ToTypedData().Http.Headers["content-type"]);
        }

        [Fact]
        public void TestObjectToTypedData_WhenBodyIsJsonAndContentTypeIsSpecified_AddsSpecifiedContentTypeHeader()
        {
            var input = new HttpResponseContext
            {
                Body = new TypedData { Json = "{\"Hello\":\"World\"}" },
                ContentType = "text/plain"
            };

            Assert.Equal("text/plain", input.ToTypedData().Http.Headers["content-type"]);
        }

        [Fact]
        public void TestObjectToTypedData_WhenBodyIsNotJsonAndNoContentType_AddsTextPlainContentTypeHeader()
        {
            var input = new HttpResponseContext
            {
                // Even though the content looks like Json,
                // the intent is to respond with a string.
                Body = new TypedData { String = "{\"Hello\":\"World\"}" }
            };

            Assert.Equal("text/plain", input.ToTypedData().Http.Headers["content-type"]);
        }

        [Fact]
        public void TestObjectToTypedData_WhenBodyIsNotJsonAndContentTypeIsSpecified_AddsSpecifiedContentTypeHeader()
        {
            var input = new HttpResponseContext
            {
                // Even though the content looks like Json,
                // the intent is to respond with a string.
                Body = new TypedData { String = "{\"Hello\":\"World\"}" },
                ContentType = "text/html"
            };

            Assert.Equal("text/html", input.ToTypedData().Http.Headers["content-type"]);
        }

        [Fact]
        public void TestObjectToTypedDataInt()
        {
            var data = 1;

            var input = (object)data;
            var expected = new TypedData
            {
                Int = data
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataDouble()
        {
            var data = 1.1;

            var input = (object)data;
            var expected = new TypedData
            {
                Double = data
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataString()
        {
            var data = "Hello World!";

            var input = (object)data;
            var expected = new TypedData
            {
                String = data
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataBytes()
        {
            var data = ByteString.CopyFromUtf8("Hello World!").ToByteArray();

            var input = (object)data;
            var expected = new TypedData
            {
                Bytes = ByteString.CopyFrom(data)
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataStream()
        {
            using(MemoryStream data = new MemoryStream(100))
            {

                var input = (object)data;
                var expected = new TypedData
                {
                    Stream = ByteString.FromStream(data)
                };

                Assert.Equal(expected, input.ToTypedData());
            }
        }

        [Fact]
        public void TestObjectToTypedDataJsonString()
        {
            var data = "{\"foo\":\"bar\"}";

            var input = (object)data;
            var expected = new TypedData
            {
                Json = data
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedDataJsonHashtable()
        {
            var data = new Hashtable { { "foo", "bar" } };

            var input = (object)data;
            var expected = new TypedData
            {
                Json = "{\"foo\":\"bar\"}"
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedData_PSObjectToJson_1()
        {
            var data = new Hashtable { { "foo", "bar" } };
            object input = PSObject.AsPSObject(data);

            var expected = new TypedData
            {
                Json = "{\"foo\":\"bar\"}"
            };

            Assert.Equal(expected, input.ToTypedData());
        }

        [Fact]
        public void TestObjectToTypedData_PSObjectToJson_2()
        {
            using (var ps = System.Management.Automation.PowerShell.Create())
            {
                object input = ps.AddScript("[pscustomobject]@{foo = 'bar'}").Invoke()[0];

                var expected = new TypedData
                {
                    Json = "{\"foo\":\"bar\"}"
                };

                Assert.Equal(expected, input.ToTypedData());
            }
        }

        [Fact]
        public void TestObjectToTypedData_PSObjectToBytes()
        {
            var data = new byte[] { 12,23,34 };
            object input = PSObject.AsPSObject(data);

            TypedData output = input.ToTypedData();

            Assert.Equal(TypedData.DataOneofCase.Bytes, output.DataCase);
            Assert.Equal(3, output.Bytes.Length);
        }

        [Fact]
        public void TestObjectToTypedData_PSObjectToStream()
        {
            using (var data = new MemoryStream(new byte[] { 12,23,34 }))
            {
                object input = PSObject.AsPSObject(data);
                TypedData output = input.ToTypedData();

                Assert.Equal(TypedData.DataOneofCase.Stream, output.DataCase);
                Assert.Equal(3, output.Stream.Length);
            }
        }

        [Fact]
        public void TestObjectToTypedData_PSObjectToString()
        {
            object input = PSObject.AsPSObject("Hello World");
            TypedData output = input.ToTypedData();

            Assert.Equal(TypedData.DataOneofCase.String, output.DataCase);
            Assert.Equal("Hello World", output.String);
        }

        #endregion
    }
}
