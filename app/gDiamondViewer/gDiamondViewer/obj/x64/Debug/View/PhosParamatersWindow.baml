   M S B A M L   `   `   `  ����I  EgDiamondViewer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null9!clr-namespace:gDiamondViewer.ViewgDiamondViewer.View  5   	   .    (gDiamondViewer.View.PhosParamatersWindow   5   	   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5   	   R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   3local!clr-namespace:gDiamondViewer.View     5   	     � Title$   Phosphorescence Paramaters��5   	   $	��160��6,   $	��300��69    � WindowStyle$ 
ToolWindow=�6E    � 
ResizeMode$ NoResize=�6^    � WindowStartupLocation$ CenterScreen=�6t   .��5	      � $��4q�6    � RowDefinitions 5
   
   �� 5      6   �� 5      6   �� 5      6   �� 5      6   �� 5      $��30 �6   6*    � ColumnDefinitions 5   
   �� 5      6!   �� 5      6!   .+�5   
   �� $��Right=�6   .��6,   Camera Delay [ms]:6@   �� 5   
   -   5         ��Name$ txtDelay��5   "   $��1��6   66   �� 5   
   $��1��6   $��Right=�6   .��69   Number of Images:6L   �� 5   
   -   5        $ txtNumImages��5   /   $��1��6   $��1��6   6G   �� 5   
   $��2��6   $��Right=�6   .��69   Spectrometer Delay [ms]:6S   �� 5   
   -   5        $ txtSpectroDelay��5   /   $��2��6   $��1��6   6J   �� 5   
   $��3��6   $��Right=�6   .��69   Number of Spectra:6M   �� 5   
   -   5        $ txtNumSpectra��5   /   $��3��6   $��1��6   6H   �� 5   
   $��4��6   $��2��6"    �� Orientation$ 
Horizontal=�66   $��Right=�6O   $��5,5,0,0q�5      .+�5      �� -   5         ��Name$ btnClose��5   1   $��80��6   $��0,0,5,0q�6    .��6Z   Close6a   �� 5       -   5        $ btnOk��5        $��80��6   .��6C   OK6G   5!      5"      5#      