// SPDX-License-Identifier: MIT
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Bwork.Authoring.Editor
{
    /// <summary>Receipt seal for owned road nodes; edited or foreign content is retained for recovery.</summary>
    public static class RoadHierarchyOwnership
    {
        public static string Fingerprint(Transform root)
        {
            if(root==null)throw new ArgumentNullException(nameof(root));
            var nodes=root.GetComponentsInChildren<Transform>(true);
            if(nodes.Length>2048)throw new InvalidDataException("Road hierarchy exceeds the finite fixture budget.");
            var ordinals=nodes.Select((node,index)=>(node,index)).ToDictionary(p=>p.node,p=>p.index);
            var text=new StringBuilder();
            void Add(string value){value=value??"";text.Append(value.Length).Append(':').Append(value).Append(';');}
            void Number(float value)
            {
                if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Nonfinite road component value.");
                // Unity scene float serialization can round the final bits; tolerate less than 0.1mm.
                Add(Math.Round(value,4,MidpointRounding.AwayFromZero).ToString("F4",CultureInfo.InvariantCulture));
            }
            void Vector(Vector3 v){Number(v.x);Number(v.y);Number(v.z);}
            void Asset(Object value)
            {
                if(value==null){Add("null");return;}
                if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string guid,out long local))
                    throw new InvalidDataException("Road contains a transient or foreign non-asset reference.");
                Add(guid);Add(local.ToString(CultureInfo.InvariantCulture));Add(AssetDatabase.GetAssetPath(value));
            }
            foreach(var node in nodes)
            {
                Add(node==root?"root":ordinals[node.parent].ToString(CultureInfo.InvariantCulture));
                Add(node==root?"Roads":node.name);Add(node==root?"True":node.gameObject.activeSelf.ToString());Add(node.childCount.ToString(CultureInfo.InvariantCulture));
                Vector(node.localPosition);Vector(node.localScale);var rotation=node.localRotation;
                Number(rotation.x);Number(rotation.y);Number(rotation.z);Number(rotation.w);
                Asset(PrefabUtility.GetCorrespondingObjectFromSource(node.gameObject));
                foreach(var component in node.GetComponents<Component>())
                {
                    if(component==null)throw new InvalidDataException("Road contains a missing script.");
                    Add(component.GetType().FullName);
                    if(component is Transform)continue;
                    if(component is MeshFilter filter)Asset(filter.sharedMesh);
                    else if(component is MeshRenderer renderer)
                    {
                        Add(renderer.enabled.ToString());Add(((int)renderer.shadowCastingMode).ToString(CultureInfo.InvariantCulture));
                        Add(renderer.receiveShadows.ToString());Add(renderer.sharedMaterials.Length.ToString(CultureInfo.InvariantCulture));
                        foreach(var material in renderer.sharedMaterials)Asset(material);
                    }
                    else if(component is MeshCollider collider)
                    {Asset(collider.sharedMesh);Add(collider.enabled.ToString());Add(collider.convex.ToString());Add(collider.isTrigger.ToString());}
                    else if(component is LODGroup lod)
                    {
                        Add(lod.enabled.ToString());Add(((int)lod.fadeMode).ToString(CultureInfo.InvariantCulture));Number(lod.size);Vector(lod.localReferencePoint);
                        foreach(var level in lod.GetLODs())
                        {
                            Number(level.screenRelativeTransitionHeight);Number(level.fadeTransitionWidth);Add(level.renderers.Length.ToString(CultureInfo.InvariantCulture));
                            foreach(var lodRenderer in level.renderers)
                            {
                                if(lodRenderer==null||!ordinals.TryGetValue(lodRenderer.transform,out int ordinal))throw new InvalidDataException("Road LOD references foreign geometry.");
                                Add(ordinal.ToString(CultureInfo.InvariantCulture));
                            }
                        }
                    }
                    else throw new InvalidDataException("Unexpected road component: "+component.GetType().Name);
                }
            }
            using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","").ToLowerInvariant();
        }
        public static void Validate(Transform root,string expectedHash,string[] assets)
        {
            if(root==null || root.name!="Roads" || !root.gameObject.activeSelf)
                throw new InvalidDataException("Road root was renamed, disabled or removed; preserve its checkpoint.");
            if(!string.IsNullOrEmpty(expectedHash))
            {
                if(Fingerprint(root)!=expectedHash)throw new InvalidDataException("Road hierarchy, transforms or component references were edited; preserve foreign content before replacement/removal.");
                return;
            }
            // Old receipts contain only flat road meshes. Admit exactly those mesh assets and
            // known surface bindings; never treat arbitrary children as owned during migration.
            if(root.GetComponents<Component>().Length!=1 || root.childCount!=assets.Length ||
                root.localPosition.sqrMagnitude>1e-10f || root.localScale!=Vector3.one || Quaternion.Angle(root.localRotation,Quaternion.identity)>.001f)
                throw new InvalidDataException("Legacy road root contains foreign or transformed content.");
            var allowed=new[]{"Asphalt","AsphaltEdge","GravelVerge","Gravel","Dirt","DirtVerge"}.Select(ProjectContext.Material).ToArray();
            var found=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(Transform child in root)
            {
                var filter=child.GetComponent<MeshFilter>();var renderer=child.GetComponent<MeshRenderer>();var collider=child.GetComponent<MeshCollider>();
                string meshPath=filter==null?"":AssetDatabase.GetAssetPath(filter.sharedMesh);
                if(child.childCount!=0 || child.GetComponents<Component>().Length!=4 || filter==null || filter.sharedMesh==null || renderer==null || collider==null ||
                    child.name!=filter.sharedMesh.name || !child.gameObject.activeSelf || !renderer.enabled || !collider.enabled || collider.isTrigger || collider.convex ||
                    child.localPosition.sqrMagnitude>1e-10f || child.localScale!=Vector3.one || Quaternion.Angle(child.localRotation,Quaternion.identity)>.001f ||
                    filter.sharedMesh!=collider.sharedMesh || !assets.Contains(meshPath) || !found.Add(meshPath) || renderer.sharedMaterials.Length!=3 ||
                    renderer.sharedMaterials.Any(m=>m==null||!allowed.Contains(AssetDatabase.GetAssetPath(m))))
                    throw new InvalidDataException("Legacy road hierarchy contains foreign or edited content; preserve its checkpoint.");
            }
        }
    }
}
