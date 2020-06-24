using System;

namespace Generator
{
    // �������� ������ (�������) ��� ��������� ������������ �����.���������
    public class GenTree
    {
        private Random _rnd = new Random();
        private Elems _elems; // ��������� ����� � �������������� �������
        private bool _frac = false; // ������������ �����?

        public GenTree(Elems e)
        {
            _elems = e;
        }

        // �������� ���� �� ������
        private void SignNode(Tree tree)
        {
            tree.State = State.Sign;
            tree.Left = new Tree(tree);
            tree.Right = new Tree(tree);
        }

        // �������� ���� � ���. ��������
        private Tree FunctionNode(Tree tree)
        {
            tree.State = State.Func;
            tree.Left = new Tree(tree);
            return tree.Left;
        }

        // ��������������� ��������� �� ����� ����������
        private void ContiniousGen(Tree tree, int depth)
        {
            if (depth < 1) return;

            var left = _rnd.Next(1, depth);
            var right = depth - left;
            GenNewTree(tree.Left, left, 0);
            GenNewTree(tree.Right, right, 0);
        }
        // �������� ������ ������ ��� �����. ���������
        private void GenNewTree(Tree tree, int hard, int curr)
        {
            if (curr < hard)
            {
                curr++;
                if ((tree.Parent.State == State.Root && _frac) || 
                    (_elems.Signs.Count > 0 && _rnd.Next(0, 101) < 70))
                {
                    SignNode(tree);
                    ContiniousGen(tree, hard - curr);
                }
                else
                    GenNewTree(FunctionNode(tree), hard, curr);
            }
        }

        public Tree Run(int hard, bool frac = false)
        {
            _frac = frac;
            var tree = new Tree(null);
            tree.Left = new Tree(tree);
            tree.State = State.Root;
            tree.Value = "ROOT";
            GenNewTree(tree.Left, hard, 0);
            return tree;
        }
    }
}