using System.Collections.Generic;

namespace ALS.CheckModule.Compare.Preparer
{
    /// <summary>
    /// Для подготовки пользовательского окружения(создание
    /// файлов-ресурсов(к примеру текстовые файлы для пользовательской программы) и др.)
    /// </summary>
    public interface IPreparer
    {
        void Prepare(string pathToUserProgram, string pathToModelProgram, List<string> input, ref bool isStdInput);
    }
}