using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace InfinitySDK.Tools.IgnoreCompile.URPShaderBuilder
{
    public class URPShaderBuilder : EditorWindow
    {
        [MenuItem ( "Assets/Create/Shader/URP Unlit Shader" )]
        static void Init ()
        {
            var window = GetWindow<URPShaderBuilder> ( "URPShaderBuilder" );
            if ( window != null )
                window.Show ();
        }

        string sampleName;

        bool createSuccess;

        void OnGUI ()
        {
            sampleName = EditorGUILayout.TextField ( "Name", sampleName );

            using ( new EditorGUI.DisabledScope ( string.IsNullOrEmpty ( sampleName ) ) )
            {
                if ( GUILayout.Button ( "Create" ) )
                {
                    Build ();

                    if ( createSuccess )
                    {
                        var window = GetWindow<URPShaderBuilder> ( "URPShaderBuilder" );
                        if ( window != null )
                        {
                            window.Close ();
                        }
                    }
                }
            }
        }

        void Build ()
        {
            var source = Path.Combine ( Application.dataPath, "Editor", "URPShaderTemplate_Unlit.txt" );

            if ( !File.Exists ( source ) )
            {
                Debug.Log ( "Source is not Exists : " + new { source } );
                return;

            }
            var destination = GetSelectedPathOrFallback ();
            destination = Path.Combine ( destination, string.Format ( "{0}.shader", sampleName ) );
            if ( File.Exists ( destination ) )
            {
                Debug.Log ( "File is Exists, Try Change the Name : " + new { sampleName } );
                return;
            }

            var contents = File.ReadAllText ( source );
            contents = contents.Replace ( "@[ShaderName]", sampleName );

            File.WriteAllText ( destination, contents, new UTF8Encoding ( true ) );
            //EditorUtility.RequestScriptReload ();
            //CodeEditor.Editor.CurrentCodeEditor.SyncAll ();
            AssetDatabase.Refresh ();

            createSuccess = true;
        }

        public static string GetSelectedPathOrFallback ()
        {
            string path = "Assets";

            foreach ( UnityEngine.Object obj in Selection.GetFiltered ( typeof ( UnityEngine.Object ), SelectionMode.Assets ) )
            {
                path = AssetDatabase.GetAssetPath ( obj );
                if ( !string.IsNullOrEmpty ( path ) && File.Exists ( path ) )
                {
                    path = Path.GetDirectoryName ( path );
                    break;
                }
            }
            return path;
        }
    }
}
