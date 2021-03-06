using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ALS.CheckModule.Compare;
using ALS.CheckModule.Compare.DataStructures;
using ALS.CheckModule.Processes;
using ALS.EntityСontext;
using Generator.MainGen;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ALS.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Student")]
    public class ChecksController : Controller
    {
        private readonly ApplicationContext _db;

        public ChecksController(ApplicationContext db)
        {
            _db = db;
        }
        
        [HttpPost]
        public async Task<IActionResult> Check(IFormFileCollection uploadedSources, [FromHeader] int variantId)
        {
            //Получим идентификатор юзера из его сессии
            var userIdentifier = int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
            //Проверим что вариант назначен пользователю
            var assignedVar =
                await _db.AssignedVariants.Include(aw => aw.Variant).ThenInclude(var => var.LaboratoryWork).FirstOrDefaultAsync(av =>
                    av.UserId == userIdentifier && av.VariantId == variantId);
            if (assignedVar != null)
            {
                //Возможно пользователь уже решил вариант
                var solution =
                    await _db.Solutions.FirstOrDefaultAsync(sol => sol.AssignedVariant == assignedVar && sol.IsSolved);
                if (solution == null)
                {
                    //Получим метод оценки
                    var evaluation = assignedVar.Variant.LaboratoryWork.Evaluation;
                    //Если не решил, то создадим нужные директории
                    var solutionDirectory = CreateDirectoriesSources(assignedVar.Variant.VariantId, variantId, userIdentifier);
                    //Cохраним его код в директорию пользователя
                    await SaveSources(solutionDirectory, uploadedSources);
                    //Создаём новое решение
                    solution = new Solution {AssignedVariant = assignedVar, IsSolved = true, SendDate = DateTime.Now, SourceCode = solutionDirectory, IsCompile = true};

                    //Возьмём пути для исполняемых файлов
                    var programFileUser = CreateExecuteDirectories(assignedVar.Variant.VariantId, variantId, userIdentifier);
                    

                    var programFileModel = new Uri((await _db.Variants.FirstOrDefaultAsync(var => var.VariantId == variantId)).LinkToModel).AbsolutePath;
                    //Создадим директорию для запуска эталонной программы программы
                    var moveModelProgram = Path.Combine(Environment.CurrentDirectory, "modelTestingFiled", $"{userIdentifier}_{variantId}");
                    Directory.CreateDirectory(moveModelProgram);
                    //Новая файл в директории
                    var newPathProgram = Path.Combine(moveModelProgram, Path.GetFileName(programFileModel));
                    System.IO.File.Copy(programFileModel, newPathProgram, true);
                    //Скомпилируем программу пользователя
                    var compiler = new ProcessCompiler(solutionDirectory, programFileUser);
                    var isCompile = await compiler.Execute(10000);
                    var extractUserDirectory = new DirectoryInfo(programFileUser).Parent.FullName;
                    //Если не скомпилировалась заносим, то в последнее решение добавим информацию что программа пользователя не была скомпилированна
                    if (isCompile != true)
                    {
                        solution.IsCompile = false;
                        solution.IsSolved = false;
                        await _db.Solutions.AddAsync(solution);
                        DeleteProgramsDirectories(extractUserDirectory, moveModelProgram);
                        await _db.SaveChangesAsync();
                        var result = compiler.CompileState;
                        //Удалим показанный путь программы
                        return BadRequest(result.Replace(solutionDirectory, null));
                    }
                    
                    //Прогоним по тестам
                    List<ResultRun> resultTests;
                    string[] testNames;
                    double[] weights;
                    try
                    {
                        //Получим входные данные для задачи
                        var gen = new Gen();

                        // testData: класс Test с полями Name, Data, Weight
                        // имя   вес   данные                    | если нужные еще какие-либо данные 
                        // test [30] : #случайноеЦелое(10,20,10) | n;
                        var testData = gen.GetTestsFromJson(assignedVar.Variant.InputDataRuns);
                        testNames = testData.Select(td => td.Name).ToArray();
                        weights = testData.Select(td => td.Weight).ToArray();
                        //Получим ограничения лабы
                        var constrainsLab = JsonConvert.DeserializeObject<Constrains>(
                            assignedVar.Variant.LaboratoryWork.Constraints,
                            new JsonSerializerSettings {DefaultValueHandling = DefaultValueHandling.Populate});
                        var variantConstrainsJson = assignedVar.Variant.Constraints;
                        if (variantConstrainsJson != null)
                        {
                            //Получим ограничения варианта
                            var constrainsVar = JsonConvert.DeserializeObject<Constrains>(
                                assignedVar.Variant.Constraints,
                                new JsonSerializerSettings {DefaultValueHandling = DefaultValueHandling.Populate});

                            //Перезапишем ограничениями лабы ограничениями варианта(которые доступны)
                            constrainsLab = OverridingConstrains(constrainsLab, constrainsVar);
                        }
                        var verification = new Verification(programFileUser, newPathProgram, constrainsLab);
                        resultTests = await verification.RunTests(testData.Select(t => t.Data).ToList());
                    }
                    catch (Exception e)
                    {
                        //Если произошло что-то не хорошее, то не будем понапрасну тратить драгоценные байты хранилища и удали исходные коды пользователя
                        Directory.Delete(solutionDirectory, true);
                        return BadRequest(e.Message);
                    }
                    finally
                    {
                        //В любом случае удалим директорию пользователя с исполняемым файлом и директорию с моделью
                        DeleteProgramsDirectories(extractUserDirectory, moveModelProgram);
                    }

                    //Сохраним сейчас чтобы добавить тестовые прогоны в БД
                    await _db.Solutions.AddAsync(solution);
                    await _db.SaveChangesAsync();
                    var counterName = 0;
                    foreach (var result in resultTests)
                    {
                        var testRun = new TestRun
                        {
                            Name = testNames[counterName++],
                            InputData = result.Input.ToArray(),
                            OutputData = result.Output.ToArray(), 
                            ResultRun = JsonConvert.SerializeObject(new { result.Time, result.Memory, result.IsCorrect, result.Comment }),
                            SolutionId = solution.SolutionId
                        };
                        await _db.TestRuns.AddAsync(testRun);
                    }

                    var countCompleteTest = resultTests.Count(rt => rt.IsCorrect);
                    solution.IsSolved = Rate(evaluation, resultTests, weights,  out var currMark);
                    assignedVar.Mark = currMark;
                    await _db.SaveChangesAsync();
                    //Выведем количество верных тестовых прогонов и комментарии к ним
                    return Ok($"{countCompleteTest} / {resultTests.Count} тестов пройдено.{FormattingResultLog(resultTests, testNames)}");
                }
                return Ok("Задача уже решена");
            }
            return BadRequest("Нет доступа");
        }
        
        private static string CreateDirectoriesSources(int lwId, int variantId, int userId)
        {
            var userDirectory = Path.Combine(Environment.CurrentDirectory, "sourceCodeUser", userId.ToString());
            if (!Directory.Exists(userDirectory))
            {
                Directory.CreateDirectory(userDirectory);
            }
            //Получим директорию решения пользователя
            var taskDirectory = Path.Combine(userDirectory, ProcessCompiler.CreatePath(lwId, variantId));
            if (!Directory.Exists(taskDirectory))
            {
                Directory.CreateDirectory(taskDirectory);
            }
            //Посмотрим на все его попытки
            var numberLastSolution = 0;
            var directoriesSolutions = Directory.GetDirectories(taskDirectory);
            if (directoriesSolutions.Length != 0)
            {
                numberLastSolution = directoriesSolutions.Max(dir => int.TryParse(Path.GetFileName(dir), out int res) ? res : 0);
            }
            //И создадим папку с новым решением
            var solutionDirectory = Path.Combine(taskDirectory, (numberLastSolution + 1).ToString());
            Directory.CreateDirectory(solutionDirectory);
            
            return solutionDirectory;
        }

        private static string CreateExecuteDirectories(int lwId, int variantId, int userId)
        {
            var userDirectory = Path.Combine(Environment.CurrentDirectory, "executeUser", userId.ToString());
            if (!Directory.Exists(userDirectory))
            {
                Directory.CreateDirectory(userDirectory);
            }
            //Получим директорию решения пользователя
            var taskDirectory = Path.Combine(userDirectory, ProcessCompiler.CreatePath(lwId, variantId));
            if (!Directory.Exists(taskDirectory))
            {
                Directory.CreateDirectory(taskDirectory);
            }

            return Path.Combine(taskDirectory, $"{ProcessCompiler.CreatePath(lwId, variantId)}.exe");
        }

        private static void DeleteProgramsDirectories(params string[] directories)
        {
            foreach (var item in directories)
            {
                Directory.Delete(item, true);
            }
        }

        private static async Task SaveSources(string directorySave, IFormFileCollection sources)
        {
            //Сохраним все файлы в директорию пользовательского решения
            foreach (var file in sources)
            {
                await using var fileStream = new FileStream(Path.Combine(directorySave, file.FileName), FileMode.Create);
                await file.CopyToAsync(fileStream);
            }
        }
        
        private static string FormattingResultLog(List<ResultRun> results, string[] testName)
        {
            var testsLog = new StringBuilder();
            for (var i = 0; i < results.Count; i++)
            {
                testsLog.Append($"\n{testName[i]}: {results[i].Comment}");
            }
            return testsLog.ToString();
        }
        
        private static bool Rate(Evaluation evaluation, List<ResultRun> results, double[] weights, out double mark)
        {
            var sumTest = results.Count;
            var testComplete = results.Count(res => res.IsCorrect);
            switch (evaluation)
            {
                case Evaluation.Strict:
                    mark = sumTest == testComplete ? 1 : 0;
                    return Math.Abs(mark - 1) < 0.0001;
                case Evaluation.NotStrict:
                    mark = Convert.ToDouble(testComplete) / sumTest;
                    return IsSolved(testComplete, sumTest);
                case Evaluation.Penalty:
                    var sumOfWeights = weights.Sum();
                    int numOfTest = 0;
                    mark = 1.0d;
                    foreach (var weight in weights)
                    {
                        if (!results[numOfTest].IsCorrect)
                        {
                            mark -= weight / sumOfWeights;
                        }
                        numOfTest++;
                    }
                    return mark >= 0.70;
                default:
                    throw new ArgumentOutOfRangeException(nameof(evaluation), evaluation, "Выбранная стратегия оценивания отсутствует");
            }
        }
        /// <summary>
        /// Если больше 70% верно, то засчитываем задачу
        /// </summary>
        /// <param name="testComplete">Пройденные тесты</param>
        /// <param name="sumTest">Всего тестов</param>
        /// <returns>Решена ли задача</returns>
        private static bool IsSolved(int testComplete, int sumTest) => (Convert.ToDouble(testComplete) / sumTest) * 100 > 70;

        private static Constrains OverridingConstrains(Constrains labConstrains, Constrains varConstrains)
        {
            varConstrains.Checker ??= labConstrains.Checker;
            varConstrains.Finaliter ??= labConstrains.Finaliter;
            varConstrains.Preparer ??= labConstrains.Preparer;
            varConstrains.Memory = varConstrains.Memory == 0 ? labConstrains.Memory : varConstrains.Memory;
            varConstrains.Time = varConstrains.Time == 0 ? labConstrains.Time : varConstrains.Time;
            return varConstrains;
        }
    }
}