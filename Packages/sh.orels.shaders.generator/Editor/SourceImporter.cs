﻿#if UNITY_2022_3_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace ORL.ShaderGenerator
{
    [ScriptedImporter(1, "orlsource")]
    public class SourceImporter : BaseTextImporter
    {
    }
}