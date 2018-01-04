//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Backends.Display.XInput
{
	public interface IInputHandler
	{
        void ButtonPressed(int button);
        void ButtonReleased(int button);

        void KeyPressed(int key);
        void KeyReleased(int key);

		void MouseMoved(int x, int y, int dx, int dy);

		bool Stop { get; set; }
		bool CursorFixed { get; }
	}
}

