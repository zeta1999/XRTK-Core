﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using XRTK.Utilities.Async;
using XRTK.Utilities.Async.Internal;
using XRTK.Utilities.Gltf.Schema;

namespace XRTK.Utilities.Gltf.Serialization
{
    public static class GltfUtility
    {
        private const uint GltfMagicNumber = 0x46546C67;

        /// <summary>
        /// Imports a glTF object from the provided uri.
        /// </summary>
        /// <param name="uri">the path to the file to load</param>
        /// <returns>New <see cref="GltfObject"/> imported from uri.</returns>
        /// <remarks>
        /// Must be called from the main thread.
        /// If the <see cref="Application.isPlaying"/> is false, then this method will run synchronously.
        /// </remarks>
        public static async Task<GltfObject> ImportGltfObjectFromPathAsync(string uri)
        {
            if (!SyncContextUtility.IsMainThread)
            {
                Debug.LogError("ImportGltfObjectFromPathAsync must be called from the main thread!");
                return null;
            }

            if (string.IsNullOrWhiteSpace(uri))
            {
                Debug.LogError("Uri is not valid.");
                return null;
            }

            GltfObject gltfObject;
            bool isGlb = false;
            bool loadAsynchronously = Application.isPlaying;

            if (loadAsynchronously) { await Awaiters.BackgroundThread; }

            if (uri.EndsWith(".gltf"))
            {
                string gltfJson = File.ReadAllText(uri);

                gltfObject = GetGltfObjectFromJson(gltfJson);

                if (gltfObject == null)
                {
                    Debug.LogError("Failed load Gltf Object from json schema.");
                    return null;
                }
            }
            else if (uri.EndsWith(".glb"))
            {
                isGlb = true;
                byte[] glbData;

#if WINDOWS_UWP

                if (loadAsynchronously)
                {
                    try
                    {
                        var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(uri);

                        if (storageFile == null)
                        {
                            Debug.LogError($"Failed to locate .glb file at {uri}");
                            return null;
                        }

                        var buffer = await Windows.Storage.FileIO.ReadBufferAsync(storageFile);

                        using (Windows.Storage.Streams.DataReader dataReader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
                        {
                            glbData = new byte[buffer.Length];
                            dataReader.ReadBytes(glbData);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message);
                        return null;
                    }
                }
                else
                {
                    glbData = UnityEngine.Windows.File.ReadAllBytes(uri);
                }
#else
                using (var stream = File.Open(uri, FileMode.Open))
                {
                    glbData = new byte[stream.Length];

                    if (loadAsynchronously)
                    {
                        await stream.ReadAsync(glbData, 0, (int)stream.Length);
                    }
                    else
                    {
                        stream.Read(glbData, 0, (int)stream.Length);
                    }
                }
#endif

                gltfObject = GetGltfObjectFromGlb(glbData);

                if (gltfObject == null)
                {
                    Debug.LogError("Failed to load GlTF Object from .glb!");
                    return null;
                }
            }
            else
            {
                Debug.LogError("Unsupported file name extension.");
                return null;
            }

            gltfObject.Uri = uri;
            int nameStart = uri.Replace("\\", "/").LastIndexOf("/", StringComparison.Ordinal) + 1;
            int nameLength = uri.Length - nameStart;
            gltfObject.Name = uri.Substring(nameStart, nameLength).Replace(isGlb ? ".glb" : ".gltf", string.Empty);

            gltfObject.LoadAsynchronously = loadAsynchronously;
            await gltfObject.ConstructAsync();

            if (gltfObject.GameObjectReference == null)
            {
                Debug.LogError("Failed to construct glTF Object.");
            }

            if (loadAsynchronously) { await Awaiters.UnityMainThread; }

            return gltfObject;
        }

        /// <summary>
        /// Gets a glTF object from the provided json string.
        /// </summary>
        /// <param name="jsonString">String defining a glTF Object.</param>
        /// <returns><see cref="GltfObject"/></returns>
        /// <remarks>Returned <see cref="GltfObject"/> still needs to be initialized using <see cref="ConstructGltf.ConstructAsync"/>.</remarks>
        public static GltfObject GetGltfObjectFromJson(string jsonString)
        {
            var gltfObject = JsonUtility.FromJson<GltfObject>(jsonString);

            if (gltfObject.extensionsRequired?.Length > 0)
            {
                Debug.LogError($"Required Extension Unsupported: {gltfObject.extensionsRequired[0]}");
                return null;
            }

            for (int i = 0; i < gltfObject.extensionsUsed?.Length; i++)
            {
                Debug.LogWarning($"Unsupported Extension: {gltfObject.extensionsUsed[i]}");
            }

            var meshPrimitiveAttributes = GetGltfMeshPrimitiveAttributes(jsonString);
            int numPrimitives = 0;

            for (var i = 0; i < gltfObject.meshes?.Length; i++)
            {
                numPrimitives += gltfObject.meshes[i]?.primitives?.Length ?? 0;
            }

            if (numPrimitives != meshPrimitiveAttributes.Count)
            {
                Debug.LogError("The number of mesh primitive attributes does not match the number of mesh primitives");
                return null;
            }

            int primitiveIndex = 0;

            for (int i = 0; i < gltfObject.meshes?.Length; i++)
            {
                for (int j = 0; j < gltfObject.meshes[i].primitives.Length; j++)
                {
                    gltfObject.meshes[i].primitives[j].Attributes = JsonUtility.FromJson<GltfMeshPrimitiveAttributes>(meshPrimitiveAttributes[primitiveIndex]);
                    primitiveIndex++;
                }
            }

            return gltfObject;
        }

        /// <summary>
        /// Gets a glTF object from the provided byte array
        /// </summary>
        /// <param name="glbData">Raw glb byte data.</param>
        /// <returns><see cref="GltfObject"/></returns>
        /// <remarks>Returned <see cref="GltfObject"/> still needs to be initialized using <see cref="ConstructGltf.ConstructAsync"/>.</remarks>
        public static GltfObject GetGltfObjectFromGlb(byte[] glbData)
        {
            const int stride = sizeof(uint);

            var magicNumber = BitConverter.ToUInt32(glbData, 0);
            var version = BitConverter.ToUInt32(glbData, stride);
            var length = BitConverter.ToUInt32(glbData, stride * 2);

            if (magicNumber != GltfMagicNumber)
            {
                Debug.LogError("File is not a glb object!");
                return null;
            }

            if (version != 2)
            {
                Debug.LogError("Glb file version mismatch! Glb must use version 2");
                return null;
            }

            if (length != glbData.Length)
            {
                Debug.LogError("Glb file size does not match the glb header defined size");
                return null;
            }

            var chunk0Length = (int)BitConverter.ToUInt32(glbData, stride * 3);
            var chunk0Type = BitConverter.ToUInt32(glbData, stride * 4);

            if (chunk0Type != (ulong)GltfChunkType.Json)
            {
                Debug.LogError("Expected chunk 0 to be Json data!");
                return null;
            }

            var jsonChunk = Encoding.ASCII.GetString(glbData, stride * 5, chunk0Length);
            var gltfObject = GetGltfObjectFromJson(jsonChunk);
            var chunk1Length = (int)BitConverter.ToUInt32(glbData, stride * 5 + chunk0Length);
            var chunk1Type = BitConverter.ToUInt32(glbData, stride * 6 + chunk0Length);

            if (chunk1Type != (ulong)GltfChunkType.BIN)
            {
                Debug.LogError("Expected chunk 1 to be BIN data!");
                return null;
            }

            Debug.Assert(gltfObject.buffers[0].byteLength == chunk1Length, "chunk 1 & buffer 0 length mismatch");

            gltfObject.buffers[0].BufferData = new byte[chunk1Length];
            Array.Copy(glbData, stride * 7 + chunk0Length, gltfObject.buffers[0].BufferData, 0, chunk1Length);

            return gltfObject;
        }

        /// <summary>
        /// Get a single Json Object using the handle provided.
        /// </summary>
        /// <param name="jsonString">The json string to search.</param>
        /// <param name="handle">The handle to look for.</param>
        /// <returns>A snippet of the json string that defines the object.</returns>
        private static string GetJsonObject(string jsonString, string handle)
        {
            var regex = new Regex($"\"{handle}\"\\s*:\\s*\\{{");
            var match = regex.Match(jsonString);
            return match.Success ? GetJsonObject(jsonString, match.Index + match.Length) : null;
        }

        private static List<string> GetGltfMeshPrimitiveAttributes(string jsonString)
        {
            var regex = new Regex("(?<Attributes>\"attributes\"[^}]+})");
            return GetGltfMeshPrimitiveAttributes(jsonString, regex);
        }

        private static List<string> GetGltfMeshPrimitiveAttributes(string jsonString, Regex regex)
        {
            var jsonObjects = new List<string>();

            if (!regex.IsMatch(jsonString))
            {
                return jsonObjects;
            }

            MatchCollection matches = regex.Matches(jsonString);

            for (var i = 0; i < matches.Count; i++)
            {
                jsonObjects.Add(matches[i].Groups["Attributes"].Captures[0].Value.Replace("\"attributes\":", string.Empty));
            }

            return jsonObjects;
        }

        /// <summary>
        /// Get a collection of glTF Extensions using the handle provided.
        /// </summary>
        /// <param name="jsonString">The json string to search.</param>
        /// <param name="handle">The handle to look for.</param>
        /// <returns>A collection of snippets with the json string that defines the object.</returns>
        private static Dictionary<string, string> GetGltfExtensionObjects(string jsonString, string handle)
        {
            // Assumption: This code assumes that a name is declared before extensions in the glTF schema.
            // This may not work for all exporters. Some exporters may fail to adhere to the standard glTF schema.
            var regex = new Regex($"(\"name\":\\s*\"\\w*\",\\s*\"extensions\":\\s*{{\\s*?)(\"{handle}\"\\s*:\\s*{{)");
            return GetGltfExtensions(jsonString, regex);
        }

        /// <summary>
        /// Get a collection of glTF Extras using the handle provided.
        /// </summary>
        /// <param name="jsonString">The json string to search.</param>
        /// <param name="handle">The handle to look for.</param>
        /// <returns>A collection of snippets with the json string that defines the object.</returns>
        private static Dictionary<string, string> GetGltfExtraObjects(string jsonString, string handle)
        {
            // Assumption: This code assumes that a name is declared before extensions in the glTF schema.
            // This may not work for all exporters. Some exporters may fail to adhere to the standard glTF schema.
            var regex = new Regex($"(\"name\":\\s*\"\\w*\",\\s*\"extras\":\\s*{{\\s*?)(\"{handle}\"\\s*:\\s*{{)");
            return GetGltfExtensions(jsonString, regex);
        }

        private static Dictionary<string, string> GetGltfExtensions(string jsonString, Regex regex)
        {
            var jsonObjects = new Dictionary<string, string>();

            if (!regex.IsMatch(jsonString))
            {
                return jsonObjects;
            }

            var matches = regex.Matches(jsonString);
            var nodeName = string.Empty;

            for (var i = 0; i < matches.Count; i++)
            {
                for (int j = 0; j < matches[i].Groups.Count; j++)
                {
                    for (int k = 0; k < matches[i].Groups[i].Captures.Count; k++)
                    {
                        nodeName = GetGltfNodeName(matches[i].Groups[i].Captures[i].Value);
                    }
                }

                if (!jsonObjects.ContainsKey(nodeName))
                {
                    jsonObjects.Add(nodeName, GetJsonObject(jsonString, matches[i].Index + matches[i].Length));
                }
            }

            return jsonObjects;
        }

        private static string GetJsonObject(string jsonString, int startOfObject)
        {
            int index;
            int bracketCount = 1;

            for (index = startOfObject; bracketCount > 0; index++)
            {
                if (jsonString[index] == '{')
                {
                    bracketCount++;
                }
                else if (jsonString[index] == '}')
                {
                    bracketCount--;
                }
            }

            return $"{{{jsonString.Substring(startOfObject, index - startOfObject)}";
        }

        private static string GetGltfNodeName(string jsonString)
        {
            jsonString = jsonString.Replace("\"name\"", string.Empty);
            jsonString = jsonString.Replace(": \"", string.Empty);
            jsonString = jsonString.Replace(":\"", string.Empty);
            jsonString = jsonString.Substring(0, jsonString.IndexOf("\"", StringComparison.Ordinal));
            return jsonString;
        }
    }
}