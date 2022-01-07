// -----------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.EventGridEdge.IotEdge
{
    internal class HttpRequestResponseSerializer
    {
        private const char SP = ' ';
        private const char CR = '\r';
        private const char LF = '\n';
        private const char ProtocolVersionSeparator = '/';
        private const string Protocol = "HTTP";
        private const string HeaderSeparator = ":";
        private const string ContentLengthHeaderName = "content-length";

        public byte[] SerializeRequest(HttpRequestMessage request)
        {
            Validate.ArgumentNotNull(request, nameof(request));
            Validate.ArgumentNotNull(request.RequestUri, nameof(request.RequestUri));

            PreProcessRequest(request);

            var builder = new StringBuilder();
            // request-line   = method SP request-target SP HTTP-version CRLF
            builder.Append(request.Method);
            builder.Append(SP);
            builder.Append(request.RequestUri.IsAbsoluteUri ? request.RequestUri.PathAndQuery : Uri.EscapeUriString(request.RequestUri.ToString()));
            builder.Append(SP);
            builder.Append($"{Protocol}{ProtocolVersionSeparator}");
            builder.Append(new Version(1, 1).ToString(2));
            builder.Append(CR);
            builder.Append(LF);

            // Headers
            builder.Append(request.Headers);

            if (request.Content != null)
            {
                long? contentLength = request.Content.Headers.ContentLength;
                if (contentLength.HasValue)
                {
                    request.Content.Headers.ContentLength = contentLength.Value;
                }

                builder.Append(request.Content.Headers);
            }

            // Headers end
            builder.Append(CR);
            builder.Append(LF);

            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        public async Task<HttpResponseMessage> DeserializeResponseAsync(HttpBufferedStream bufferedStream, CancellationToken cancellationToken)
        {
            var httpResponse = new HttpResponseMessage();
            await SetResponseStatusLineAsync(httpResponse, bufferedStream, cancellationToken);
            await SetHeadersAndContentAsync(httpResponse, bufferedStream, cancellationToken);
            return httpResponse;
        }

        private static async Task SetHeadersAndContentAsync(HttpResponseMessage httpResponse, HttpBufferedStream bufferedStream, CancellationToken cancellationToken)
        {
            IList<string> headers = new List<string>();
            string line = await bufferedStream.ReadLineAsync(cancellationToken);
            while (!string.IsNullOrWhiteSpace(line))
            {
                headers.Add(line);
                line = await bufferedStream.ReadLineAsync(cancellationToken);
            }

            httpResponse.Content = new StreamContent(bufferedStream);
            foreach (string header in headers)
            {
                if (string.IsNullOrWhiteSpace(header))
                {
                    // headers end
                    break;
                }

                int headerSeparatorPosition = header.IndexOf(HeaderSeparator, StringComparison.OrdinalIgnoreCase);
                if (headerSeparatorPosition <= 0)
                {
                    throw new HttpRequestException($"Header is invalid {header}.");
                }

                string headerName = header.Substring(0, headerSeparatorPosition).Trim();
                string headerValue = header.Substring(headerSeparatorPosition + 1).Trim();

                bool headerAdded = httpResponse.Headers.TryAddWithoutValidation(headerName, headerValue);
                if (!headerAdded)
                {
                    if (string.Equals(headerName, ContentLengthHeaderName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!long.TryParse(headerValue, out long contentLength))
                        {
                            throw new HttpRequestException($"Header value is invalid for {headerName}.");
                        }

                        await httpResponse.Content.LoadIntoBufferAsync(contentLength);
                    }

                    httpResponse.Content.Headers.TryAddWithoutValidation(headerName, headerValue);
                }
            }
        }

        private static async Task SetResponseStatusLineAsync(HttpResponseMessage httpResponse, HttpBufferedStream bufferedStream, CancellationToken cancellationToken)
        {
            string statusLine = await bufferedStream.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(statusLine))
            {
                throw new HttpRequestException("Response is empty.");
            }

            string[] statusLineParts = statusLine.Split(new[] { SP }, 3);
            if (statusLineParts.Length < 3)
            {
                throw new HttpRequestException("Status line is not valid.");
            }

            string[] httpVersion = statusLineParts[0].Split(new[] { ProtocolVersionSeparator }, 2);
            if (httpVersion.Length < 2 || !Version.TryParse(httpVersion[1], out Version versionNumber))
            {
                throw new HttpRequestException($"Version is not valid {statusLineParts[0]}.");
            }

            httpResponse.Version = versionNumber;

            if (!Enum.TryParse(statusLineParts[1], out HttpStatusCode statusCode))
            {
                throw new HttpRequestException($"StatusCode is not valid {statusLineParts[1]}.");
            }

            httpResponse.StatusCode = statusCode;
            httpResponse.ReasonPhrase = statusLineParts[2];
        }

        private static void PreProcessRequest(HttpRequestMessage request)
        {
            if (string.IsNullOrEmpty(request.Headers.Host))
            {
                request.Headers.Host = $"{request.RequestUri.DnsSafeHost}:{request.RequestUri.Port}";
            }

            request.Headers.ConnectionClose = true;
        }
    }
}