using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Refract.Core;
using UnityEngine;

namespace Refract.Tests
{
    /// <summary>
    /// 레벨 데이터 검증. Assets/Resources/Levels 의 모든 JSON에 대해 자동으로 실행된다.
    /// 레벨을 추가하면 테스트도 자동으로 늘어난다 → "풀 수 없는 레벨은 배포되지 않는다".
    /// </summary>
    public class LevelDataTests
    {
        private static string LevelsDir => Path.Combine(Application.dataPath, "Resources", "Levels");

        private static IEnumerable<TestCaseData> LevelFiles()
        {
            foreach (var path in Directory.GetFiles(LevelsDir, "*.json"))
                yield return new TestCaseData(path).SetName("Level_" + Path.GetFileNameWithoutExtension(path));
        }

        private static LevelData Load(string path) => LevelLoader.Parse(File.ReadAllText(path));

        [Test]
        public void AtLeastOneLevelExists()
        {
            Assert.IsNotEmpty(Directory.GetFiles(LevelsDir, "*.json"));
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void HasNoStructuralErrors(string path)
        {
            var errors = LevelValidator.Validate(Load(path));
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void StoredSolution_ClearsLevel(string path)
        {
            var solved = SolutionChecker.IsSolvedBySolution(Load(path), out var reason);
            Assert.IsTrue(solved, reason);
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void IsNotClearedWithoutMirrors(string path)
        {
            Assert.IsFalse(SolutionChecker.IsClearedWithoutMirrors(Load(path)),
                "거울 없이도 클리어되는 레벨입니다.");
        }

        [Test]
        public void Validator_DetectsBrokenLevel()
        {
            // 검증기 자체가 제대로 오류를 잡는지 확인 (광원 없음 + 장치 겹침 + 보드 밖)
            var broken = new LevelData
            {
                id = "BROKEN", width = 5, height = 5, mirrors = 0,
                pieces = new List<PieceData>
                {
                    new PieceData { type = "Receiver", x = 1, y = 1 },
                    new PieceData { type = "Wall", x = 1, y = 1 },
                    new PieceData { type = "Wall", x = 9, y = 9 },
                }
            };

            var errors = LevelValidator.Validate(broken);

            Assert.AreEqual(3, errors.Count, string.Join("\n", errors));
        }
    }
}
