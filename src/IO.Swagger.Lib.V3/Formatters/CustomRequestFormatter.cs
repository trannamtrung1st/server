using DataTransferObjects.MetadataDTOs;
using DataTransferObjects.ValueDTOs;
using IO.Swagger.Lib.V3.SerializationModifiers;
using IO.Swagger.Lib.V3.SerializationModifiers.Mappers.ValueMappers;
using IO.Swagger.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace IO.Swagger.Lib.V3.Formatters
{
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using AdminShellNS;
    using Microsoft.AspNetCore.JsonPatch;
    using Newtonsoft.Json;

    public class CustomRequestFormatter : InputFormatter
    {
        public CustomRequestFormatter()
        {
            SupportedMediaTypes.Clear();
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/json"));
        }

        public override bool CanRead(InputFormatterContext context)
        {
            if (typeof(JsonPatchDocument).IsAssignableFrom(context.ModelType))
            {
                return true;
            }

            return false;
        }

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {
            Type type = context.ModelType;
            var request = context.HttpContext.Request;
            object? result = null;

            if (type == typeof(JsonPatchDocument))
            {
                using var reader = new StreamReader(request.Body);
                var bodyStr = await reader.ReadToEndAsync();
                result = JsonConvert.DeserializeObject<JsonPatchDocument>(bodyStr);
            }

            return await InputFormatterResult.SuccessAsync(result);
        }
    }
}