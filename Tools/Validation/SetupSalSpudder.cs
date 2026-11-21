var boss = UnityEngine.GameObject.Find("Sal Spudder");
if (boss == null) throw new System.Exception("Sal Spudder scene object not found");
var attackClip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/@Animations/Boss/Boss1_Attack.anim");
var idleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/@Animations/Boss/Boss1_Idle.anim");
var flyClip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/@Animations/Boss_Bullet/Sal Spudder_Bullet_Shoot.anim");
var hitClip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/@Animations/Boss_Bullet/Sal Spudder_Bullet_Death.anim");
foreach (var clip in new[] { attackClip, idleClip, flyClip, hitClip })
{
    var frames = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip)
        .SelectMany(b=>UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip,b)).ToArray();
    if (frames.Length == 0 || frames.Any(k=>k.value==null)) throw new System.Exception("Missing sprites: " + clip.name);
    var settings = UnityEditor.AnimationUtility.GetAnimationClipSettings(clip);
    settings.loopTime = clip == idleClip || clip == flyClip;
    UnityEditor.AnimationUtility.SetAnimationClipSettings(clip, settings);
    UnityEditor.EditorUtility.SetDirty(clip);
}
float releaseTime = 9f / attackClip.frameRate;
var events = UnityEditor.AnimationUtility.GetAnimationEvents(attackClip).Where(e=>e.functionName!="FireProjectile").ToList();
events.Add(new UnityEngine.AnimationEvent { functionName="FireProjectile", time=releaseTime });
UnityEditor.AnimationUtility.SetAnimationEvents(attackClip, events.OrderBy(e=>e.time).ToArray());

string dirtPath = "Assets/@Prefabs/Sal Spudder_Bullet.prefab";
string wormPath = "Assets/@Prefabs/Sal Spudder_Worm_Temporary.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(dirtPath);
try
{
    var body = root.GetComponent<UnityEngine.Rigidbody2D>();
    if (body == null) body = root.AddComponent<UnityEngine.Rigidbody2D>();
    body.gravityScale = 0f;
    body.linearDamping = 0f;
    body.constraints = UnityEngine.RigidbodyConstraints2D.FreezeRotation;
    body.collisionDetectionMode = UnityEngine.CollisionDetectionMode2D.Continuous;
    body.interpolation = UnityEngine.RigidbodyInterpolation2D.None;
    var collider = root.GetComponent<UnityEngine.Collider2D>();
    if (collider == null)
    {
        var circle = root.AddComponent<UnityEngine.CircleCollider2D>();
        var bounds = root.GetComponent<UnityEngine.SpriteRenderer>().sprite.bounds;
        circle.radius = UnityEngine.Mathf.Max(.05f, UnityEngine.Mathf.Min(bounds.extents.x,bounds.extents.y)*.8f);
        circle.offset = bounds.center;
        collider = circle;
    }
    collider.isTrigger = true;
    var bullet = root.GetComponent<Boss_Bullet>();
    if (bullet == null) bullet = root.AddComponent<Boss_Bullet>();
    var settings = new UnityEditor.SerializedObject(bullet);
    settings.FindProperty("hitAnimation").objectReferenceValue = hitClip;
    settings.ApplyModifiedPropertiesWithoutUndo();
    root.GetComponent<UnityEngine.Animator>().cullingMode = UnityEngine.AnimatorCullingMode.AlwaysAnimate;
    root.GetComponent<UnityEngine.SpriteRenderer>().sortingOrder = 12;
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, dirtPath);
    if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(wormPath) == null)
    {
        root.name = "Sal Spudder_Worm_Temporary";
        // 임시 지렁이 식별용 색상입니다. 패링 판정은 구현하지 않습니다.
        root.GetComponent<UnityEngine.SpriteRenderer>().color = new UnityEngine.Color(1f,.65f,.8f,1f);
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, wormPath);
    }
}
finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }

var parent = UnityEngine.GameObject.Find("Boss Bullet Pools");
if (parent == null)
{
    parent = new UnityEngine.GameObject("Boss Bullet Pools");
    UnityEditor.Undo.RegisterCreatedObjectUndo(parent,"Create boss pools");
}
Boss_Bullet_Pool ConfigurePool(string name, string prefabPath)
{
    var child = parent.transform.Find(name);
    if (child == null)
    {
        var go = new UnityEngine.GameObject(name);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go,"Create boss pool");
        go.transform.SetParent(parent.transform,false);
        child = go.transform;
    }
    var pool = child.GetComponent<Boss_Bullet_Pool>();
    if (pool == null) pool = UnityEditor.Undo.AddComponent<Boss_Bullet_Pool>(child.gameObject);
    var settings = new UnityEditor.SerializedObject(pool);
    settings.FindProperty("projectilePrefab").objectReferenceValue = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath).GetComponent<Boss_Bullet>();
    settings.ApplyModifiedProperties();
    return pool;
}
var dirtPool = ConfigurePool("Dirt Pool",dirtPath);
var wormPool = ConfigurePool("Worm Pool",wormPath);
var behaviour = boss.GetComponent<Boss_SalSpudder>();
if (behaviour == null) behaviour = UnityEditor.Undo.AddComponent<Boss_SalSpudder>(boss);
var bossSettings = new UnityEditor.SerializedObject(behaviour);
bossSettings.FindProperty("shootPos").objectReferenceValue = boss.transform.Find("ShootPos");
bossSettings.FindProperty("dirtPool").objectReferenceValue = dirtPool;
bossSettings.FindProperty("wormPool").objectReferenceValue = wormPool;
bossSettings.FindProperty("releaseNormalizedTime").floatValue = releaseTime/attackClip.length;
bossSettings.ApplyModifiedProperties();
boss.GetComponent<UnityEngine.Animator>().cullingMode = UnityEngine.AnimatorCullingMode.AlwaysAnimate;
UnityEditor.EditorUtility.SetDirty(boss.GetComponent<UnityEngine.Animator>());
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(boss.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(boss.scene);
return new { scene=boss.scene.path, releaseTime, frame=10, dirtPath, wormPath, pools=new[]{dirtPool.name,wormPool.name} };
