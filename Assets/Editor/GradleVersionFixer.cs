using UnityEditor;
using UnityEngine;
using System.IO;
using System;

[InitializeOnLoad]
public class GradleVersionFixer
{
    private const string GRADLE_WRAPPER_PATH = "Temp/PlayServicesResolverGradle/gradle/wrapper/gradle-wrapper.properties";
    private const string GRADLE_PROPERTIES_PATH = "Temp/PlayServicesResolverGradle/gradle.properties";
    private const string OLD_GRADLE_URL = "https\\://services.gradle.org/distributions/gradle-5.1.1-bin.zip";
    private const string OLD_GRADLE_URL_2 = "https\\://services.gradle.org/distributions/gradle-7.6.4-bin.zip";
    private const string OLD_GRADLE_URL_3 = "https\\://services.gradle.org/distributions/gradle-8.4-bin.zip";
    private const string NEW_GRADLE_URL = "https\\://services.gradle.org/distributions/gradle-8.11.1-bin.zip";

    // JVM 힙 사이즈 설정 (12GB - Java heap space 에러 방지)
    private const string GRADLE_PROPERTIES_CONTENT = @"org.gradle.jvmargs=-Xmx12288M -XX:MaxMetaspaceSize=2048M -XX:+HeapDumpOnOutOfMemoryError -XX:+UseG1GC -XX:+UseStringDeduplication
org.gradle.parallel=true
org.gradle.daemon=false
org.gradle.caching=true
org.gradle.daemon.performance.disable-logging=true
android.useAndroidX=true
android.enableJetifier=true
";

    private const string GRADLE_OPTS = "-Xmx12288M -XX:MaxMetaspaceSize=2048M -XX:+UseG1GC";

    static GradleVersionFixer()
    {
        // 환경 변수 설정 (Gradle 시작 전에 적용)
        SetGradleEnvironmentVariables();

        // EDM4U가 실행되기 전에 Gradle 폴더 미리 생성
        PreCreateGradleWrapper();

        EditorApplication.update += CheckAndFixGradleVersion;
    }

    private static void PreCreateGradleWrapper()
    {
        try
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string resolverDir = Path.Combine(projectRoot, "Temp/PlayServicesResolverGradle");
            string wrapperDir = Path.Combine(resolverDir, "gradle/wrapper");
            string wrapperPropertiesPath = Path.Combine(wrapperDir, "gradle-wrapper.properties");

            // 폴더가 없으면 생성
            if (!Directory.Exists(wrapperDir))
            {
                Directory.CreateDirectory(wrapperDir);
            }

            // gradle-wrapper.properties 미리 생성 (Gradle 8.11.1)
            string wrapperContent = @"distributionBase=GRADLE_USER_HOME
distributionPath=wrapper/dists
distributionUrl=https\://services.gradle.org/distributions/gradle-8.11.1-bin.zip
zipStoreBase=GRADLE_USER_HOME
zipStorePath=wrapper/dists
";
            if (!File.Exists(wrapperPropertiesPath) || !File.ReadAllText(wrapperPropertiesPath).Contains("gradle-8.11.1"))
            {
                File.WriteAllText(wrapperPropertiesPath, wrapperContent);
                Debug.Log("[GradleVersionFixer] Pre-created gradle-wrapper.properties with Gradle 8.11.1");
            }

            // gradle.properties 미리 생성
            string propertiesPath = Path.Combine(resolverDir, "gradle.properties");
            if (!File.Exists(propertiesPath) || !File.ReadAllText(propertiesPath).Contains("-Xmx12288M"))
            {
                File.WriteAllText(propertiesPath, GRADLE_PROPERTIES_CONTENT);
                Debug.Log("[GradleVersionFixer] Pre-created gradle.properties with 8GB heap");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GradleVersionFixer] Failed to pre-create Gradle wrapper: {e.Message}");
        }
    }

    private static void SetGradleEnvironmentVariables()
    {
        try
        {
            string currentOpts = Environment.GetEnvironmentVariable("GRADLE_OPTS") ?? "";
            if (!currentOpts.Contains("-Xmx12288M"))
            {
                Environment.SetEnvironmentVariable("GRADLE_OPTS", GRADLE_OPTS);
                Environment.SetEnvironmentVariable("JAVA_OPTS", GRADLE_OPTS);
                Debug.Log("[GradleVersionFixer] Set GRADLE_OPTS environment variable with 8GB heap");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GradleVersionFixer] Failed to set environment variables: {e.Message}");
        }
    }

    private static void CheckAndFixGradleVersion()
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string wrapperPath = Path.Combine(projectRoot, GRADLE_WRAPPER_PATH);
        string propertiesPath = Path.Combine(projectRoot, GRADLE_PROPERTIES_PATH);

        // Gradle 버전 업데이트
        if (File.Exists(wrapperPath))
        {
            try
            {
                string content = File.ReadAllText(wrapperPath);

                bool updated = false;
                if (content.Contains(OLD_GRADLE_URL))
                {
                    content = content.Replace(OLD_GRADLE_URL, NEW_GRADLE_URL);
                    updated = true;
                }
                if (content.Contains(OLD_GRADLE_URL_2))
                {
                    content = content.Replace(OLD_GRADLE_URL_2, NEW_GRADLE_URL);
                    updated = true;
                }
                if (content.Contains(OLD_GRADLE_URL_3))
                {
                    content = content.Replace(OLD_GRADLE_URL_3, NEW_GRADLE_URL);
                    updated = true;
                }
                if (updated)
                {
                    File.WriteAllText(wrapperPath, content);
                    Debug.Log("[GradleVersionFixer] Updated Gradle version to 8.11.1");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GradleVersionFixer] Failed to update Gradle version: {e.Message}");
            }
        }

        // JVM 힙 사이즈 설정 (gradle.properties)
        string resolverDir = Path.Combine(projectRoot, "Temp/PlayServicesResolverGradle");
        if (Directory.Exists(resolverDir))
        {
            try
            {
                bool needsUpdate = false;

                if (!File.Exists(propertiesPath))
                {
                    needsUpdate = true;
                }
                else
                {
                    string existingContent = File.ReadAllText(propertiesPath);
                    // 힙 사이즈가 8192M가 아니면 업데이트
                    if (!existingContent.Contains("-Xmx12288M"))
                    {
                        needsUpdate = true;
                    }
                }

                if (needsUpdate)
                {
                    File.WriteAllText(propertiesPath, GRADLE_PROPERTIES_CONTENT);
                    Debug.Log("[GradleVersionFixer] Updated gradle.properties with 8GB heap size");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GradleVersionFixer] Failed to update gradle.properties: {e.Message}");
            }
        }
    }
}
