﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ORL.Drawers
{
    public class SetKeywordDrawer: IDrawer
    {
        public bool MatchDrawer(MaterialProperty property)
        {
            return property.displayName.StartsWith("%SetKeyword");
        }
        
        private Regex _regex = new Regex(@"%SetKeyword\((?<texture>[\w]+)+,\s*(?<keyword>[\w]+)\)");
        
        public string[] PersistentKeys => Array.Empty<string>();

        public bool OnGUI(MaterialEditor editor, MaterialProperty[] properties, MaterialProperty property, int index, ref Dictionary<string, object> uiState, Func<bool> next)
        {
            if (EditorGUI.indentLevel == -1) return true;
            
            var match = _regex.Match(property.displayName);
            var keyword = match.Groups["keyword"].Value;
            var texture = match.Groups["texture"].Value;
            foreach (Material material in editor.targets)
            {
                if (material.GetTexture(texture) != null)
                    material.EnableKeyword(keyword);
                else
                    material.DisableKeyword(keyword);
            }

            return true;
        }
    }
}