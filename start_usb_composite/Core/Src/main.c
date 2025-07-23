/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2025 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"
#include "can.h"
#include "dma.h"
#include "i2c.h"
#include "tim.h"
#include "usart.h"
#include "usb_otg.h"
#include "gpio.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */
#include "usb_device.h"
#include "usbd_hid_custom.h"
#include "usbd_hid_custom_if.h"
/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */
extern USBD_HandleTypeDef hUsbDevice;
extern CAN_HandleTypeDef hcan1;
extern TIM_HandleTypeDef htim5;
/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/

/* USER CODE BEGIN PV */
uint8_t test_process1[64] = {0x03, 0x80, 0x00, 0x00, 0x03, 0x21, 0x11, 0x22, 0x33, 0x44,0x55, 0x66, 0x77, 0x88,};
uint8_t test_process2[64] = {0x03, 0x80, 0x00, 0x00, 0x01, 0x23, 0x78, 0x20, 0x78, 0x21,0x78, 0x20, 0x78, 0x21,};
CAN_TxHeaderTypeDef TxHeader;
CAN_RxHeaderTypeDef RxHeader;
CAN_FilterTypeDef  	sFilterConfig;
uint32_t txMailbox;




/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
/* USER CODE BEGIN PFP */
void Process_HID_Frames(void);
void CanTx_init(uint32_t id, uint8_t dlc, uint8_t *data);
void CanRx_init(void);

/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */
void Process_HID_Frames(void) {
    uint8_t frame[HID_FRAME_SIZE];

    while (HID_Frame_Read(frame)) {
        // Xử lý từng frame ở đây
        // Ví dụ:

        if (frame[0] == 2) {
        	uint32_t id = (frame[2]<< 8) | frame[3];
        	CanTx_init(id, frame[4], &frame[5]);
        	//USBD_CUSTOM_HID_SendReport(&hUsbDevice, test_process1, sizeof(test_process1));
        }

    }
}

void CanTx_init(uint32_t id, uint8_t dlc, uint8_t *data){
	uint8_t buffer[8] = {0};
	uint32_t txMailbox;

	TxHeader.StdId = id;
	TxHeader.IDE = CAN_ID_STD;
	TxHeader.RTR = CAN_RTR_DATA;
	TxHeader.DLC = dlc;
	TxHeader.TransmitGlobalTime = DISABLE;

	memcpy(buffer, data, dlc);

	HAL_CAN_AddTxMessage(&hcan1, &TxHeader, buffer, &txMailbox);
}

void CanRx_init(void){

		//=================can filter==============//
		sFilterConfig.FilterBank = 0;
		sFilterConfig.FilterMode = CAN_FILTERMODE_IDMASK;
		sFilterConfig.FilterScale = CAN_FILTERSCALE_32BIT;
		sFilterConfig.FilterIdHigh = 0x0000;
		sFilterConfig.FilterIdLow = 0;
		sFilterConfig.FilterMaskIdHigh = 0x0000;
		sFilterConfig.FilterMaskIdLow = 0;
		sFilterConfig.FilterFIFOAssignment = CAN_RX_FIFO0;
		sFilterConfig.FilterActivation = ENABLE;
		HAL_CAN_ConfigFilter(&hcan1, &sFilterConfig);
		HAL_CAN_ActivateNotification(&hcan1,CAN_IT_RX_FIFO0_MSG_PENDING);
}
/* USER CODE END 0 */

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{

  /* USER CODE BEGIN 1 */

  /* USER CODE END 1 */

  /* MCU Configuration--------------------------------------------------------*/

  /* Reset of all peripherals, Initializes the Flash interface and the Systick. */
  HAL_Init();

  /* USER CODE BEGIN Init */

  /* USER CODE END Init */

  /* Configure the system clock */
  SystemClock_Config();

  /* USER CODE BEGIN SysInit */

  /* USER CODE END SysInit */

  /* Initialize all configured peripherals */
  MX_GPIO_Init();
  MX_DMA_Init();
  MX_CAN1_Init();
  MX_USB_OTG_FS_PCD_Init();
  MX_UART5_Init();
  MX_I2C1_Init();
  MX_TIM1_Init();
  MX_TIM2_Init();
  MX_TIM4_Init();
  MX_TIM5_Init();
  /* USER CODE BEGIN 2 */
  MX_USB_DEVICE_Init();
  HAL_TIM_Base_Start(&htim5);
  HAL_TIM_Base_Start_IT(&htim4);
  HAL_CAN_Start(&hcan1);
  CanRx_init();
  //__HAL_TIM_GET_COUNTER(&htim5)
  /* USER CODE END 2 */

  /* Infinite loop */
  /* USER CODE BEGIN WHILE */
  while (1)
  {
    /* USER CODE END WHILE */

    /* USER CODE BEGIN 3 */
	  Process_HID_Frames();

//	      if ((now - last_toggle_time) >= 1000)
//	      {
//	          last_toggle_time = now;
//	          HAL_GPIO_TogglePin(GPIOA, GPIO_PIN_7);
//	      }

//	  if((hUsbDevice.dev_state == USBD_STATE_CONFIGURED) && tx_ok){
//		  hoang[3]+=1;
//		  tx_ok = 0;  // Chặn gửi tiếp cho đến khi callback báo xong
//		  USBD_CUSTOM_HID_SendReport(&hUsbDevice, hoang, sizeof(hoang));
//	  }
//	  HAL_Delay(500);
  }
  /* USER CODE END 3 */
}

/**
  * @brief System Clock Configuration
  * @retval None
  */
void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};

  /** Configure the main internal regulator output voltage
  */
  __HAL_RCC_PWR_CLK_ENABLE();
  __HAL_PWR_VOLTAGESCALING_CONFIG(PWR_REGULATOR_VOLTAGE_SCALE1);

  /** Initializes the RCC Oscillators according to the specified parameters
  * in the RCC_OscInitTypeDef structure.
  */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSE;
  RCC_OscInitStruct.HSEState = RCC_HSE_ON;
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSE;
  RCC_OscInitStruct.PLL.PLLM = 4;
  RCC_OscInitStruct.PLL.PLLN = 168;
  RCC_OscInitStruct.PLL.PLLP = RCC_PLLP_DIV2;
  RCC_OscInitStruct.PLL.PLLQ = 7;
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  /** Initializes the CPU, AHB and APB buses clocks
  */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK
                              |RCC_CLOCKTYPE_PCLK1|RCC_CLOCKTYPE_PCLK2;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV4;
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV2;

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_5) != HAL_OK)
  {
    Error_Handler();
  }
}

/* USER CODE BEGIN 4 */
void HAL_CAN_RxFifo0MsgPendingCallback(CAN_HandleTypeDef *hcan)
{
    uint8_t rxData[8];
    uint8_t usbFrame[64] = {0}; // clear buffer
    uint8_t frameLen = 0;
    memset(usbFrame, 0, sizeof(usbFrame));

    if (HAL_CAN_GetRxMessage(&hcan1, CAN_RX_FIFO0, &RxHeader, rxData) == HAL_OK)
    {
        HAL_GPIO_TogglePin(GPIOA, GPIO_PIN_7); // báo nhận

        // Byte 0: CMD
        usbFrame[0] = 0x03;

        // Byte 1: DLC (4-bit high), FrameType (4-bit low)
        uint8_t dlc = RxHeader.DLC & 0x0F;
        uint8_t frameType = 0;
        if (RxHeader.IDE == CAN_ID_EXT)
            frameType = 1;
        else if (RxHeader.RTR == CAN_RTR_REMOTE)
            frameType = 2;

        usbFrame[1] = (dlc << 4) | (frameType & 0x0F);

        // Byte 2~5: CAN ID (big-endian)
        uint32_t canId = (RxHeader.IDE == CAN_ID_EXT) ? RxHeader.ExtId : RxHeader.StdId;
        usbFrame[2] = (canId >> 24) & 0xFF;
        usbFrame[3] = (canId >> 16) & 0xFF;
        usbFrame[4] = (canId >> 8) & 0xFF;
        usbFrame[5] = canId & 0xFF;

        // Byte 6~6+DLC: data
        for (uint8_t i = 0; i < dlc; ++i)
        {
            usbFrame[6 + i] = rxData[i];
        }

        frameLen = 6 + dlc;

        // ✅ Gửi đúng độ dài thực tế (frameLen), hoặc 64 nếu host yêu cầu cố định
        USBD_CUSTOM_HID_SendReport(&hUsbDevice, usbFrame, frameLen);
    }
}


/* USER CODE END 4 */

/**
  * @brief  This function is executed in case of error occurrence.
  * @retval None
  */
void Error_Handler(void)
{
  /* USER CODE BEGIN Error_Handler_Debug */
  /* User can add his own implementation to report the HAL error return state */
  __disable_irq();
  while (1)
  {
  }
  /* USER CODE END Error_Handler_Debug */
}

#ifdef  USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */
